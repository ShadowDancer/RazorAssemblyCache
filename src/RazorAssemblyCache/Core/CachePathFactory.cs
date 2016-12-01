using System;
using System.IO;
using Microsoft.Extensions.Options;
using RazorAssemblyCache.Options;

namespace RazorAssemblyCache.Core
{
    /// <summary>
    ///     Creates oaths for resources
    /// </summary>
    internal class CachePathFactory
    {
        public CachePathFactory(IOptions<RazorAssemblyCacheOptions> options)
        {
            if (options.Value.CacheDirectory == null)
            {
                throw new InvalidOperationException(
                    "Invalid directory configured in " + nameof(RazorAssemblyCacheOptions));
            }

            CacheDirectory = options.Value.CacheDirectory;
        }

        private string CacheDirectory { get; }

        public CachePathMaker CreatePathWith(string relativeViewPath)
        {
            if (relativeViewPath.StartsWith("/"))
            {
                relativeViewPath = relativeViewPath.Substring(1);
            }

            var path = Path.Combine(CacheDirectory, relativeViewPath.Replace("/", "\\"));
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
                return _viewPath + ".pdb";
            }
        }
    }
}