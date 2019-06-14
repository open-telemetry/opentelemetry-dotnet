using System;
using System.Threading;

namespace LoggingTracer
{

    public static class Logger
    {
        static DateTime startTime = DateTime.UtcNow;

        public static void Log(string s)
        {
            Console.WriteLine($"OT {MillisSinceStart(),8} {Thread.CurrentThread.ManagedThreadId,2}: {s}");
        }

        private static int MillisSinceStart()
        {
            return (int)DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
        }
    }
}
