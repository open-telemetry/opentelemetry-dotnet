namespace LoggingTracer.ConsoleApp
{
    using System.Threading;
    using OpenTelemetry.Trace;

    public class Program
    {
        private static ITracer tracer = new LoggingTracer();

        public static void Main(string[] args)
        {

            var builder = tracer.SpanBuilder("Main (span1)");
            using (var scope = builder.StartScopedSpan())
            {
                Thread.Sleep(100);
                Foo();
            }
        }

        private static void Foo()
        {
            var builder = tracer.SpanBuilder("Foo (span2)");
            using (var scope = builder.StartScopedSpan())
            {
                tracer.CurrentSpan.SetAttribute("myattribute", "mvalue");
                Thread.Sleep(100);
            }
        }
    }
}
