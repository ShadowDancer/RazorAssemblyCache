// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RazorAssemblyCache.Utilities;

namespace RazorAssemblyCache.RoslynCompilationService
{
    /// <summary>
    ///     A type that uses Roslyn to compile C# content.
    /// </summary>
    internal class RoslynCompilationServiceWithDump : ICompilationService
    {
        // error CS0234: The type or namespace name 'C' does not exist in the namespace 'N' (are you missing
        // an assembly reference?)
        private const string CS0234 = nameof(CS0234);
        // error CS0246: The type or namespace name 'T' could not be found (are you missing a using directive
        // or an assembly reference?)
        private const string CS0246 = nameof(CS0246);
        private readonly Action<RoslynCompilationContext> _compilationCallback;

        private readonly CSharpCompiler _compiler;
        private readonly IFileProvider _fileProvider;
        private readonly ILogger _logger;

        /// <summary>
        ///     Initalizes a new instance of the <see cref="RoslynCompilationServiceWithDump" /> class.
        /// </summary>
        /// <param name="compiler">The <see cref="CSharpCompiler" />.</param>
        /// <param name="optionsAccessor">Accessor to <see cref="RazorViewEngineOptions" />.</param>
        /// <param name="fileProviderAccessor">The <see cref="IRazorViewEngineFileProviderAccessor" />.</param>
        /// <param name="loggerFactory">The <see cref="RoslynCompilationServiceWithDump" />.</param>
        public RoslynCompilationServiceWithDump(
            CSharpCompiler compiler,
            IRazorViewEngineFileProviderAccessor fileProviderAccessor,
            IOptions<RazorViewEngineOptions> optionsAccessor,
            ILoggerFactory loggerFactory, CachePathFactory factory)
        {
            _compiler = compiler;
            _factory = factory;
            _fileProvider = fileProviderAccessor.FileProvider;
            _compilationCallback = optionsAccessor.Value.CompilationCallback;
            _logger = loggerFactory.CreateLogger<DefaultRoslynCompilationService>();
        }

        /// <inheritdoc />
        public CompilationResult Compile(RelativeFileInfo fileInfo, string compilationContent)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            if (compilationContent == null)
            {
                throw new ArgumentNullException(nameof(compilationContent));
            }

            _logger.GeneratedCodeToAssemblyCompilationStart(fileInfo.RelativePath);
            var startTimestamp = _logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;

            var assemblyName = Path.GetRandomFileName();
            var compilation = CreateCompilation(compilationContent, assemblyName);

            using (var assemblyStream = new MemoryStream())
            {
                using (var pdbStream = new MemoryStream())
                {
                    var result = compilation.Emit(
                        assemblyStream,
                        pdbStream,
                        options: _compiler.EmitOptions);

                    if (!result.Success)
                    {
                        return GetCompilationFailedResult(
                            fileInfo.RelativePath,
                            compilationContent,
                            assemblyName,
                            result.Diagnostics);
                    }

                    assemblyStream.Seek(0, SeekOrigin.Begin);
                    pdbStream.Seek(0, SeekOrigin.Begin);

                    var assembly = LoadAssembly(assemblyStream, pdbStream);
                    var type = assembly.GetExportedTypes().FirstOrDefault(a => !a.IsNested);

                    _logger.GeneratedCodeToAssemblyCompilationEnd(fileInfo.RelativePath, startTimestamp);

                    SaveAssembly(fileInfo, assemblyStream, pdbStream);

                    return new CompilationResult(type);
                }
            }
        }

        private CSharpCompilation CreateCompilation(string compilationContent, string assemblyName)
        {
            var sourceText = SourceText.From(compilationContent, Encoding.UTF8);
            var syntaxTree = _compiler.CreateSyntaxTree(sourceText).WithFilePath(assemblyName);
            var compilation = _compiler
                .CreateCompilation(assemblyName)
                .AddSyntaxTrees(syntaxTree);
            compilation = ExpressionRewriter.Rewrite(compilation);

            var compilationContext = new RoslynCompilationContext(compilation);
            _compilationCallback(compilationContext);
            compilation = compilationContext.Compilation;
            return compilation;
        }

        // Internal for unit testing
        internal CompilationResult GetCompilationFailedResult(
            string relativePath,
            string compilationContent,
            string assemblyName,
            IEnumerable<Diagnostic> diagnostics)
        {
            var diagnosticGroups = diagnostics
                .Where(IsError)
                .GroupBy(diagnostic => GetFilePath(relativePath, diagnostic), StringComparer.Ordinal);

            var failures = new List<CompilationFailure>();
            foreach (var group in diagnosticGroups)
            {
                var sourceFilePath = group.Key;
                string sourceFileContent;
                if (string.Equals(assemblyName, sourceFilePath, StringComparison.Ordinal))
                {
                    // The error is in the generated code and does not have a mapping line pragma
                    sourceFileContent = compilationContent;
                    sourceFilePath = Resources.GeneratedCodeFileName;
                }
                else
                {
                    sourceFileContent = ReadFileContentsSafely(_fileProvider, sourceFilePath);
                }

                string additionalMessage = null;
                if (group.Any(g =>
                    string.Equals(CS0234, g.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(CS0246, g.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    additionalMessage = Resources.FormatCompilation_DependencyContextIsNotSpecified(
                        "preserveCompilationContext",
                        "buildOptions",
                        "project.json");
                }

                var compilationFailure = new CompilationFailure(
                    sourceFilePath,
                    sourceFileContent,
                    compilationContent,
                    group.Select(GetDiagnosticMessage),
                    additionalMessage);

                failures.Add(compilationFailure);
            }

            return new CompilationResult(failures);
        }

        private static string GetFilePath(string relativePath, Diagnostic diagnostic)
        {
            if (diagnostic.Location == Location.None)
            {
                return relativePath;
            }

            return diagnostic.Location.GetMappedLineSpan().Path;
        }

        private static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error;
        }

        public static Assembly LoadAssembly(MemoryStream assemblyStream, MemoryStream pdbStream)
        {
            var assembly =
#if NET451
                Assembly.Load(assemblyStream.ToArray(), pdbStream.ToArray());
#else
                System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(assemblyStream, pdbStream);
#endif
            return assembly;
        }

        private static string ReadFileContentsSafely(IFileProvider fileProvider, string filePath)
        {
            var fileInfo = fileProvider.GetFileInfo(filePath);
            if (fileInfo.Exists)
            {
                try
                {
                    using (var reader = new StreamReader(fileInfo.CreateReadStream()))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch
                {
                    // Ignore any failures
                }
            }

            return null;
        }

        private static DiagnosticMessage GetDiagnosticMessage(Diagnostic diagnostic)
        {
            var mappedLineSpan = diagnostic.Location.GetMappedLineSpan();
            return new DiagnosticMessage(
                diagnostic.GetMessage(),
                CSharpDiagnosticFormatter.Instance.Format(diagnostic),
                mappedLineSpan.Path,
                mappedLineSpan.StartLinePosition.Line + 1,
                mappedLineSpan.StartLinePosition.Character + 1,
                mappedLineSpan.EndLinePosition.Line + 1,
                mappedLineSpan.EndLinePosition.Character + 1);
        }

        #region AddedContent

        /// <summary>
        ///     Saves compiled assembly to file from bytestream
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="ms"></param>
        /// <param name="assemblySymbols"></param>
        private void SaveAssembly(RelativeFileInfo fileInfo, MemoryStream ms, MemoryStream assemblySymbols)
        {
            try
            {
                var cachePath = _factory.CreatePathWith(fileInfo.RelativePath);
                ms.Seek(0, SeekOrigin.Begin);
                using (var writer = new FileStream(cachePath.ForAssembly(), FileMode.Create))
                {
                    ms.CopyTo(writer);
                }

                assemblySymbols.Seek(0, SeekOrigin.Begin);
                using (var writer = new FileStream(cachePath.ForSymbols(), FileMode.Create))
                {
                    assemblySymbols.CopyTo(writer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(4272, ex, "Failed to save compiled assembly for RazorView: {0}", fileInfo.RelativePath);
                throw;
            }
        }


        private readonly CachePathFactory _factory;

        // Also added _factory imports for constructors

        #endregion
    }
}