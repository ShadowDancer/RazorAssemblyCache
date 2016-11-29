using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace RazorAssemblyCache
{
    /// <summary>
    ///     Creates oaths for resources
    /// </summary>
    public class RazorAssemblyCachePathFactory
    {
        public RazorAssemblyCachePathFactory(IHostingEnvironment env)
        {
            CacheDirectory = Path.Combine(env.ContentRootPath, "compiler-cache");
        }

        private string CacheDirectory { get; }

        public CachePathMaker CreatePathWith(string relativeViewPath)
        {
            var path = CacheDirectory + relativeViewPath.Replace("/", "\\");
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return new CachePathMaker(path);
        }

        public class CachePathMaker
        {
            private readonly string _viewPath;

            public CachePathMaker(string viewPath)
            {
                _viewPath = viewPath;
            }

            public string ForAssembly()
            {
                return _viewPath + ".dll";
            }

            public string ForSymbols()
            {
                return _viewPath + ".pkb";
            }

            public string ForHash()
            {
                return _viewPath + ".sha";
            }
        }
    }
}