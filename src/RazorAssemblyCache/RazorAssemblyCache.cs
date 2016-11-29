using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;

namespace RazorAssemblyCache
{
    public class RazorAssemblyCache : ICompilerCache
    {
        private readonly ICompilerCache _cache;

        public RazorAssemblyCache(ICompilerCache cache, RazorAssemblyCachePathFactory cachePathFactory)
        {
            _cache = cache;
            CachePathMaker = cachePathFactory;
        }

        public RazorAssemblyCachePathFactory CachePathMaker { get; }

        public CompilerCacheResult GetOrAdd(string relativePath, Func<RelativeFileInfo, CompilationResult> compile)
        {
            var cachePath = CachePathMaker.CreatePathWith(relativePath);


            var assemblyPath = cachePath.ForAssembly();
            if (File.Exists(assemblyPath))
            {
                var symobls = cachePath.ForSymbols();
                Assembly assembly;
                if (File.Exists(symobls))
                {
                    assembly = Assembly.Load(File.ReadAllBytes(assemblyPath), File.ReadAllBytes(symobls));
                }
                else
                {
                    assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
                }


                var type = assembly.GetExportedTypes().FirstOrDefault(a => !a.IsNested);
                return new CompilerCacheResult(relativePath, new CompilationResult(type));
            }

            return _cache.GetOrAdd(relativePath, compile);
        }
    }
}