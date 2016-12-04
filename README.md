[![Build status](https://ci.appveyor.com/api/projects/status/wrdd3quo4awsefej/branch/master?svg=true)](https://ci.appveyor.com/project/ShadowDancer/razorassemblycache/branch/master)
[![NuGet Pre Release](https://img.shields.io/nuget/vpre/RazorAssemblyCache.svg?style=plastic)](https://www.nuget.org/packages/RazorAssemblyCache/)

# Hello aspnet developer!

Have You asked Yourself why my shiny .net core application takes 6 seconds to start on machine with ssd, and 12 core processor? Well no? Check in trace mode - razor view compiling takes most of that time.

Or maybe you miss temporary aspnet files?

Ye, there is razor-precompile tooling, but it is fun on production, not during development. So here it is - library that solves this problem. Compiled views are cached on disk, and then loaded on next startup. For WebApplication template I've managed to cut startup time by 3/4. Nice, huh?

What about downsides:
- it's one big hack (RoslynCompilationService is copied from mvc repository - if it changes we're doomed!!!)
- doesn't work with embedded views (such views are ignored, and will not be cached)
- there probably are problems when you change depenency of cached stuff - you have to remove assembly by yourself

It works quite nicely (times on my machine in debug configuration):
1. Full framework w/o cache: ~7500ms, cached: ~2500ms
2. Core framework w/o cache: ~3450ms, cached: ~2100ms

There is also nuget package. Just run `Install-Package RazorAssemblyCache -Pre` and put this in your startup:

```
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (_env.IsDevelopment())
            {
                // You should use this only for development work
                // If you want to speed up production better use precompilation tool!
                services.AddRazorAssemblyCache();
            }
            // Add framework services.
            services.AddMvc();
        }
```


# TL;DR
Speed up WebApplication startup time with saving compiled razor views on disk!