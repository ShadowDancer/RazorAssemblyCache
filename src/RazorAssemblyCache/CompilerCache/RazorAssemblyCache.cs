using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using RazorAssemblyCache.Core;

namespace RazorAssemblyCache.CompilerCache
{
    /// <summary>
    ///     Implementation of ICompilerCache that checks if assembly is cached on disk before calling compiler
    /// </summary>
    internal class RazorAssemblyCache : ICompilerCache
    {
        private readonly ICompilerCache _cache;
        private readonly IFileProvider _contentFileProvider;
        private readonly ILogger _logger;

        public RazorAssemblyCache(ICompilerCache cache, CachePathFactory cachePathFactory,
            IFileProvider contentFileProvider, ILogger logger)
        {
            _cache = cache;
            _contentFileProvider = contentFileProvider;
            _logger = logger;
            CachePathMaker = cachePathFactory;
        }

        private CachePathFactory CachePathMaker { get; }

        public CompilerCacheResult GetOrAdd(string relativePath, Func<RelativeFileInfo, CompilationResult> compile)
        {
            try
            {
                var cachePath = CachePathMaker.CreatePathWith(relativePath);
                var assemblyPath = cachePath.ForAssembly();
                var shouldCache = true;
                if (File.Exists(assemblyPath))
                {
                    var cacheCreationTime = File.GetLastWriteTime(assemblyPath);
                    var viewFileInfo = _contentFileProvider.GetFileInfo(relativePath);
                    if (viewFileInfo.PhysicalPath == null)
                    {
                        // File is embedded in assembly, idk how to get assembly modification time
                        // I guess that here should be hash instead of modification time, 
                        // because assembly may be recompiled, but view inside may not change
                        shouldCache = false;
                        _logger.LogDebug("Skipping cache for {0}, because source file is missing", relativePath);
                    }

                    if (viewFileInfo.Exists && viewFileInfo.LastModified > cacheCreationTime)
                    {
                        // cache is outdatted, compiler will override it
                        shouldCache = false;
                        _logger.LogDebug("Skipping outdated cache for {0}", relativePath);
                    }

                    if (shouldCache)
                    {
                        var result = ReadCachedCompilationFromDisk(assemblyPath, cachePath.ForSymbols());
                        _logger.LogDebug("Cache for {0}, loaded from {1}", relativePath, assemblyPath);
                        return new CompilerCacheResult(relativePath, result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(4273, ex, "Something went wrong when tried to get cache for: {0}", relativePath);
            }

            return _cache.GetOrAdd(relativePath, compile);
        }


        /// <summary>
        ///     Loads assembly from disk, finds non nested type (as RoslynCompilationService does),
        ///     and returns it
        /// </summary>
        /// <param name="assemblyPath">Path to file containing view assembly serialized to bytestream</param>
        /// <param name="symbolsPath">Path to file containing symbols serialized to bytestream</param>
        /// <returns></returns>
        private static CompilationResult ReadCachedCompilationFromDisk(string assemblyPath, string symbolsPath)
        {
            var assemblyData = File.ReadAllBytes(assemblyPath);
            Assembly assembly;
            if (File.Exists(symbolsPath))
            {
                var symbolData = File.ReadAllBytes(symbolsPath);
                assembly = Assembly.Load(assemblyData, symbolData);
            }
            else
            {
                assembly = Assembly.Load(assemblyData);
            }

            var cachedCompilationResult =
                new CompilationResult(assembly.GetExportedTypes().FirstOrDefault(a => !a.IsNested));
            return cachedCompilationResult;
        }
    }
}