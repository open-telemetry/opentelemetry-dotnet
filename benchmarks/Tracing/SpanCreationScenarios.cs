using OpenTelemetry.Trace;

namespace BenchmarkSdk.Tracing
{
    internal class SpanCreationScenarios
    {
        public static ISpan CreateSpan(ITracer tracer)
        {
            var span = tracer.SpanBuilder("span").StartSpan();
            span.End();
            return span;
        }

        public static ISpan CreateSpan_Attributes(ITracer tracer)
        {
            var span = tracer.SpanBuilder("span")
                .StartSpan();
            span.SetAttribute("attribute1", "1");
            span.SetAttribute("attribute2", 2);
            span.SetAttribute("attribute3", 3.0);
            span.SetAttribute("attribute4", false);
            span.End();
            return span;
        }

        public static ISpan CreateSpan_Propagate(ITracer tracer)
        {
            var span = tracer.SpanBuilder("span")
                .StartSpan();
            using (tracer.WithSpan(span))
            {

            }
            return span;
        }
    }
}
