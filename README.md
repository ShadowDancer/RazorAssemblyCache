[![Build status](https://ci.appveyor.com/api/projects/status/wrdd3quo4awsefej/branch/master?svg=true)](https://ci.appveyor.com/project/ShadowDancer/razorassemblycache/branch/master)

# Hello internet!

Have You asked Yourself why my shiny .net core application takes 6 seconds to start on my shiny ssd, and 12 core processor? Well no? Check in trace mode - razor view compiling takes most of that time.

Or maybe you miss temporary aspnet files?

Ye, there is razor-precompile tooling, but it is fun for production, not for developers. So here it is - library that solves this problem. Compiled views are cached on disk, and then loaded on next startup. For WebApplication template I've managed to cut startup time from 6 to 1.2 seconds. Nice, huh?

What what about downsides:
- it's one big hack (RoslynCompilationService is copied from mvc repository - if it changes we're doomed!!!)
- doesn't work on core framework (netcoreapp)
- doesn't work with embedded views (such views are ignored, and will not be cached)
- there probably are problems that cannot be easly solved, probably something with changing dependences of cached stuff

Atm this is alpha-alpha-alpha version, supported only for fullframework. Currently I am testing this on myself ;)

# TL;DR
Speed up WebApplication startup time with saving compiled razor views on disk!