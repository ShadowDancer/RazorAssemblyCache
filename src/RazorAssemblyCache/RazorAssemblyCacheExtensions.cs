using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace RazorAssemblyCache
{
    public static class RazorAssemblyCacheExtensions
    {
        public static void UseRazorAssemblyCache(this IServiceCollection services)
        {
            services.AddSingleton<ICompilerCacheProvider, RazorAssemblyCacheProviderWrapper>();
            services.AddSingleton<DefaultCompilerCacheProvider>();
            services.AddSingleton<ICompilationService, RoslynCompilationServiceWithDump>();
            services.AddSingleton<RazorAssemblyCachePathFactory>();
        }
    }
}