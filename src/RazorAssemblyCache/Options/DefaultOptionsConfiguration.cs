using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace RazorAssemblyCache.Options
{
    public class DefaultOptionsConfiguration : IConfigureOptions<RazorAssemblyCacheOptions>
    {
        private readonly IHostingEnvironment _hostingEnvironment;

        public DefaultOptionsConfiguration(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public void Configure(RazorAssemblyCacheOptions options)
        {
            if (options.CacheDirectory == null)
            {
                options.CacheDirectory = Path.Combine(_hostingEnvironment.ContentRootPath, "temp", "viewCache");
            }
        }
    }
}