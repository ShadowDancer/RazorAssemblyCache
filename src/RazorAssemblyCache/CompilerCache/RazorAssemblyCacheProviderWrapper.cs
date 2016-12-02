using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.Logging;
using RazorAssemblyCache.Utilities;

namespace RazorAssemblyCache.CompilerCache
{
    internal class RazorAssemblyCacheProviderWrapper : ICompilerCacheProvider
    {
        public RazorAssemblyCacheProviderWrapper(DefaultCompilerCacheProvider defaultCacheProvider,
            CachePathFactory factory, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger(typeof(RazorAssemblyCache));
            Cache = new RazorAssemblyCache(defaultCacheProvider.Cache, factory, env.ContentRootFileProvider, logger);
        }

        public ICompilerCache Cache { get; }
    }
}