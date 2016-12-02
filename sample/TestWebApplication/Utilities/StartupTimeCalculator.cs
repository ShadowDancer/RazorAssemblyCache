using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TestWebApplication.Utilities
{
    public class StartupTimeCalculator
    {
        private static Stopwatch sw = new Stopwatch();
        public static void Start()
        {
            sw.Start();
        }

        public static void Stop()
        {
            sw.Stop();

        }

        public static float GetTime()
        {
            Stop();
            return sw.ElapsedMilliseconds;
        }
    }
}
