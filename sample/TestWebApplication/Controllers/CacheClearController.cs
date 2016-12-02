using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RazorAssemblyCache.Options;
using System.IO;

namespace TestWebApplication.Controllers
{
    public class CacheController : Controller
    {
        private string _path;
        public CacheController(IOptions<RazorAssemblyCacheOptions> options)
        {
            _path = options.Value.CacheDirectory;
        }


        public IActionResult Clear()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, true);
            }
            return Content("Cache cleared, restart your application!");
        }
    }
}
