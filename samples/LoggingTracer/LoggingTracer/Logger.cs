namespace LoggingTracer
{
    using System;
    using System.Threading;

    public static class Logger
    {
        private static DateTime startTime = DateTime.UtcNow;

        static Logger() => PrintHeader();

        public static void PrintHeader() => Console.WriteLine("MsSinceStart | ThreadId | API");

        public static void Log(string s)
            => Console.WriteLine($"{MillisSinceStart(),12} | {Thread.CurrentThread.ManagedThreadId,8} | {s}");

        private static int MillisSinceStart()
            => (int)DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
    }
}
