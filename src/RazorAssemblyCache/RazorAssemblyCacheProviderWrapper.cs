using Microsoft.AspNetCore.Mvc.Razor.Internal;

namespace RazorAssemblyCache
{
    public class RazorAssemblyCacheProviderWrapper : ICompilerCacheProvider
    {
        private readonly ICompilerCache _cache = null;
        private readonly DefaultCompilerCacheProvider _defaultCompilerCacheProvider;
        private readonly RazorAssemblyCachePathFactory _factory;

        public RazorAssemblyCacheProviderWrapper(DefaultCompilerCacheProvider defaultCompilerCacheProvider,
            RazorAssemblyCachePathFactory factory)
        {
            _defaultCompilerCacheProvider = defaultCompilerCacheProvider;
            _factory = factory;
        }


        public ICompilerCache Cache => _cache ?? new RazorAssemblyCache(_defaultCompilerCacheProvider.Cache, _factory);
    }
}