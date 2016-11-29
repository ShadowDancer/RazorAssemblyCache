# Hello internet!

Have You asked Yourself why my shiny .net core application takes 6 seconds to start on my shiny ssd, and 12 core processor? Well no? Check in trace mode - razor view compiling takes most of that time.

Or maybe you miss temporary aspnet files?

Ye, there is razor-precompile tooling, but it is fun for production, not for developers. So here it is - library that solves this problem. Compiled views are cached on disk, and then loaded on next startup. For WebApplication template I've managed to cut startup time from 6 to 1.2 seconds. Nice, huh?

What what about downsides:
- it's one big hack
- atm you have to clear cache manually
- there probably are problems that cannot be easly solved

Atm this is alpha-alpha-alpha version, supported only for fullframework. Just for benchmarking purposes.

# TL;DR
Speed up WebApplication startup time with saving compiled razor views on disk!