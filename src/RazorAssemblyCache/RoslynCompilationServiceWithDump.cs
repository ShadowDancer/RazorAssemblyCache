using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RazorAssemblyCache
{
    /// <summary>
    /// A type that Razor uses to compile cshtml views. 
    /// I had to copy it, because I cannot extend it
    /// </summary>
    public class RoslynCompilationServiceWithDump : ICompilationService
    {
        #region AddedContent

        /// <summary>
        /// Saves compiled assembly to file from bytestream
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
                using (FileStream writer = new FileStream(cachePath.ForAssembly(), FileMode.Create))
                {
                    ms.CopyTo(writer);
                }

                assemblySymbols.Seek(0, SeekOrigin.Begin);
                using (FileStream writer = new FileStream(cachePath.ForSymbols(), FileMode.Create))
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


        private readonly RazorAssemblyCachePathFactory _factory;

        // Also added _factory imports for constructors
        #endregion
        private readonly Lazy<List<MetadataReference>> _applicationReferences;
        private readonly Action<RoslynCompilationContext> _compilationCallback;
        private readonly CSharpCompilationOptions _compilationOptions;
        private readonly DependencyContext _dependencyContext;
        private readonly IFileProvider _fileProvider;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, AssemblyMetadata> _metadataFileCache =
            new ConcurrentDictionary<string, AssemblyMetadata>(StringComparer.OrdinalIgnoreCase);

        private readonly CSharpParseOptions _parseOptions;

        /// <summary>
        ///     Initalizes a new instance of the
        ///     <see cref="T:Microsoft.AspNetCore.Mvc.Razor.Internal.RoslynCompilationServiceWithDump" /> class.
        /// </summary>
        /// <param name="environment">The <see cref="T:Microsoft.AspNetCore.Hosting.IHostingEnvironment" />.</param>
        /// <param name="optionsAccessor">Accessor to <see cref="T:Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions" />.</param>
        /// <param name="fileProviderAccessor">
        ///     The
        ///     <see cref="T:Microsoft.AspNetCore.Mvc.Razor.Internal.IRazorViewEngineFileProviderAccessor" />.
        /// </param>
        /// <param name="loggerFactory">The <see cref="T:Microsoft.Extensions.Logging.ILoggerFactory" />.</param>
        /// <param name="factory"></param>
        public RoslynCompilationServiceWithDump(IHostingEnvironment environment,
            IOptions<RazorViewEngineOptions> optionsAccessor, IRazorViewEngineFileProviderAccessor fileProviderAccessor,
            ILoggerFactory loggerFactory, RazorAssemblyCachePathFactory factory)
            : this(GetDependencyContext(environment), optionsAccessor.Value, fileProviderAccessor, loggerFactory, factory)
        {
        }

        internal RoslynCompilationServiceWithDump(DependencyContext dependencyContext,
            RazorViewEngineOptions viewEngineOptions, IRazorViewEngineFileProviderAccessor fileProviderAccessor,
            ILoggerFactory loggerFactory, RazorAssemblyCachePathFactory factory)
        {
            _dependencyContext = dependencyContext;
            _factory = factory;
            _applicationReferences = new Lazy<List<MetadataReference>>(GetApplicationReferences);
            _fileProvider = fileProviderAccessor.FileProvider;
            _compilationCallback = viewEngineOptions.CompilationCallback;
            _parseOptions = viewEngineOptions.ParseOptions;
            _compilationOptions = viewEngineOptions.CompilationOptions;
            _logger = loggerFactory.CreateLogger<RoslynCompilationServiceWithDump>();
        }

        /// <inheritdoc />
        public CompilationResult Compile(RelativeFileInfo fileInfo, string compilationContent)
        {
            if (fileInfo == null)
                throw new ArgumentNullException("fileInfo");
            if (compilationContent == null)
                throw new ArgumentNullException("compilationContent");
            var startTimestamp = _logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0L;
            var randomFileName = Path.GetRandomFileName();
            var text1 = SourceText.From(compilationContent, Encoding.UTF8, SourceHashAlgorithm.Sha1);
            var str = randomFileName;
            var options1 = _parseOptions;
            var path = str;
            var cancellationToken = new CancellationToken();
            var text2 = CSharpSyntaxTree.ParseText(text1, options1, path, cancellationToken);
            var assemblyName = randomFileName;
            var compilationOptions = _compilationOptions;
            var syntaxTreeArray = new SyntaxTree[1] {text2};
            var metadataReferenceList = _applicationReferences.Value;
            var options2 = compilationOptions;
            var compilationContext =
                new RoslynCompilationContext(
                    Rewrite(CSharpCompilation.Create(assemblyName, syntaxTreeArray, metadataReferenceList, options2)));
            _compilationCallback(compilationContext);
            var compilation = compilationContext.Compilation;
            using (var ms = new MemoryStream())
            {
                using (var assemblySymbols = new MemoryStream())
                {
                    var emitResult = compilation.Emit(ms, assemblySymbols, null, null, null,
                        new EmitOptions(false, DebugInformationFormat.PortablePdb, null, null, 0, 0UL, false,
                            new SubsystemVersion(), null, false, false), null, new CancellationToken());
                    if (!emitResult.Success)
                    {
                        if (!compilation.References.Any() && !_applicationReferences.Value.Any())
                            throw new InvalidOperationException("Hey");
                        return GetCompilationFailedResult(fileInfo.RelativePath, compilationContent, randomFileName,
                            emitResult.Diagnostics);
                    }
                    ms.Seek(0L, SeekOrigin.Begin);
                    assemblySymbols.Seek(0L, SeekOrigin.Begin);
                    var type = LoadStream(ms, assemblySymbols).GetExportedTypes().FirstOrDefault(a => !a.IsNested);
                    SaveAssembly(fileInfo, ms, assemblySymbols);
                    return new CompilationResult(type);
                }
            }
        }

        private Assembly LoadStream(MemoryStream ms, MemoryStream assemblySymbols)
        {
            return Assembly.Load(ms.ToArray(), assemblySymbols.ToArray());
        }

        private CSharpCompilation Rewrite(CSharpCompilation compilation)
        {
            var syntaxTreeList = new List<SyntaxTree>();
            foreach (var syntaxTree1 in compilation.SyntaxTrees)
            {
                var expressionRewriter = new ExpressionRewriter(compilation.GetSemanticModel(syntaxTree1, true));
                var syntaxTree2 =
                    syntaxTree1.WithRootAndOptions(
                        expressionRewriter.Visit(syntaxTree1.GetRoot(new CancellationToken())), syntaxTree1.Options);
                syntaxTreeList.Add(syntaxTree2);
            }
            return compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(syntaxTreeList);
        }

        internal CompilationResult GetCompilationFailedResult(string relativePath, string compilationContent,
            string assemblyName, IEnumerable<Diagnostic> diagnostics)
        {
            var groupings = diagnostics.Where(IsError)
                .GroupBy(diagnostic => GetFilePath(relativePath, diagnostic), StringComparer.Ordinal);
            var compilationFailureList = new List<CompilationFailure>();
            foreach (var source in groupings)
            {
                var str = source.Key;
                string sourceFileContent;
                if (string.Equals(assemblyName, str, StringComparison.Ordinal))
                {
                    sourceFileContent = compilationContent;
                    str = "Generated Code";
                }
                else
                    sourceFileContent = ReadFileContentsSafely(_fileProvider, str);
                var compilationFailure = new CompilationFailure(str, sourceFileContent, compilationContent,
                    source.Select(GetDiagnosticMessage));
                compilationFailureList.Add(compilationFailure);
            }
            return new CompilationResult(compilationFailureList);
        }

        private static string GetFilePath(string relativePath, Diagnostic diagnostic)
        {
            if (diagnostic.Location == Location.None)
                return relativePath;
            return diagnostic.Location.GetMappedLineSpan().Path;
        }

        private List<MetadataReference> GetApplicationReferences()
        {
            var metadataReferenceList = new List<MetadataReference>();
            if (_dependencyContext == null)
                return metadataReferenceList;
            for (var index = 0; index < _dependencyContext.CompileLibraries.Count; ++index)
            {
                var compilationLibrary = _dependencyContext.CompileLibraries[index];
                IEnumerable<string> source;
                try
                {
                    source = compilationLibrary.ResolveReferencePaths();
                }
                catch (InvalidOperationException ex)
                {
                    continue;
                }
                metadataReferenceList.AddRange(source.Select(CreateMetadataFileReference));
            }
            return metadataReferenceList;
        }

        private MetadataReference CreateMetadataFileReference(string path)
        {
            return _metadataFileCache.GetOrAdd(path, _ =>
            {
                using (var fileStream = File.OpenRead(path))
                    return
                        AssemblyMetadata.Create(ModuleMetadata.CreateFromStream(fileStream,
                            PEStreamOptions.PrefetchMetadata));
            }).GetReference(null, new ImmutableArray<string>(), false, path, null);
        }

        private static bool IsError(Diagnostic diagnostic)
        {
            if (!diagnostic.IsWarningAsError)
                return diagnostic.Severity == DiagnosticSeverity.Error;
            return true;
        }

        private static string ReadFileContentsSafely(IFileProvider fileProvider, string filePath)
        {
            var fileInfo = fileProvider.GetFileInfo(filePath);
            if (fileInfo.Exists)
            {
                try
                {
                    using (var streamReader = new StreamReader(fileInfo.CreateReadStream()))
                        return streamReader.ReadToEnd();
                }
                catch
                {
                }
            }
            return null;
        }

        private static DiagnosticMessage GetDiagnosticMessage(Diagnostic diagnostic)
        {
            var mappedLineSpan = diagnostic.Location.GetMappedLineSpan();
            var message = diagnostic.GetMessage(null);
            var formattedMessage = CSharpDiagnosticFormatter.Instance.Format(diagnostic, null);
            var path = mappedLineSpan.Path;
            var linePosition = mappedLineSpan.StartLinePosition;
            var startLine = linePosition.Line + 1;
            linePosition = mappedLineSpan.StartLinePosition;
            var startColumn = linePosition.Character + 1;
            linePosition = mappedLineSpan.EndLinePosition;
            var endLine = linePosition.Line + 1;
            linePosition = mappedLineSpan.EndLinePosition;
            var endColumn = linePosition.Character + 1;
            return new DiagnosticMessage(message, formattedMessage, path, startLine, startColumn, endLine, endColumn);
        }

        private static DependencyContext GetDependencyContext(IHostingEnvironment environment)
        {
            if (environment.ApplicationName != null)
                return DependencyContext.Load(Assembly.Load(new AssemblyName(environment.ApplicationName)));
            return null;
        }
    }
}