using System;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorAssemblyCache.CompilerCache;
using RazorAssemblyCache.Options;
using RazorAssemblyCache.RoslynCompilationService;
using RazorAssemblyCache.Utilities;

namespace RazorAssemblyCache
{
    public static class RazorAssemblyCacheExtensions
    {
        /// <summary>
        ///     Cache temporary razor files. By default files are cached in ContentRootDirectory
        /// </summary>
        /// <param name="services"></param>
        public static void AddRazorAssemblyCache(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddSingleton<IConfigureOptions<RazorAssemblyCacheOptions>, DefaultOptionsConfiguration>();
            RegisterServies(services);
        }

        /// <summary>
        ///     Cache temporary razor files. By default files are cached in ContentRootDirectory
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure">Lambda used to configure RazorAssemblyCacheOptions</param>
        public static void AddRazorAssemblyCache(this IServiceCollection services,
            Action<RazorAssemblyCacheOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            services.Configure(configure);

            RegisterServies(services);
        }

        private static void RegisterServies(IServiceCollection services)
        {
            services.AddSingleton<ICompilerCacheProvider, RazorAssemblyCacheProviderWrapper>();
            services.AddSingleton<DefaultCompilerCacheProvider>();
            services.AddSingleton<ICompilationService, RoslynCompilationServiceWithDump>();
            services.AddSingleton<CachePathFactory>();
        }
    }
}