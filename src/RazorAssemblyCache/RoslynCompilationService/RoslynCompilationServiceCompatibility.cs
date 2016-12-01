using System;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace RazorAssemblyCache.RoslynCompilationService
{
    /// <summary>
    /// Compatibilty layer for <see cref="RoslynCompilationServiceWithDump"/>
    /// so I can copy it to my solution without commenging out logging and other inernal stuff
    /// </summary>
    internal static class LoggerExtensions
    {
        private static readonly Action<ILogger, string, Exception> _generatedCodeToAssemblyCompilationStart;
        private static readonly Action<ILogger, string, double, Exception> _generatedCodeToAssemblyCompilationEnd;

        private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond/(double) Stopwatch.Frequency;

        static LoggerExtensions()
        {
            _generatedCodeToAssemblyCompilationStart = LoggerMessage.Define<string>(
                LogLevel.Debug,
                1,
                "Compilation of the generated code for the Razor file at '{FilePath}' started.");

            _generatedCodeToAssemblyCompilationEnd = LoggerMessage.Define<string, double>(
                LogLevel.Debug,
                2,
                "Compilation of the generated code for the Razor file at '{FilePath}' completed in {ElapsedMilliseconds}ms.");
        }

        public static void GeneratedCodeToAssemblyCompilationStart(this ILogger logger, string filePath)
        {
            _generatedCodeToAssemblyCompilationStart(logger, filePath, null);
        }

        public static void GeneratedCodeToAssemblyCompilationEnd(this ILogger logger, string filePath,
            long startTimestamp)
        {
            // Don't log if logging wasn't enabled at start of request as time will be wildly wrong.
            if (startTimestamp != 0)
            {
                var currentTimestamp = Stopwatch.GetTimestamp();
                var elapsed = new TimeSpan((long) (TimestampToTicks*(currentTimestamp - startTimestamp)));
                _generatedCodeToAssemblyCompilationEnd(logger, filePath, elapsed.TotalMilliseconds, null);
            }
        }
    }

    public static class Resources
    {
        public static string GeneratedCodeFileName => "Generated Code";

        public static string FormatCompilation_DependencyContextIsNotSpecified(string preservecompilationcontext,
            string buildoptions, string projectJson)
        {
            return string.Format(CultureInfo.CurrentCulture,
                "One or more compilation references are missing. Possible causes include a missing '{0}' property under '{1}' in the application's {2}.",
                preservecompilationcontext, buildoptions, projectJson);
        }
    }
}