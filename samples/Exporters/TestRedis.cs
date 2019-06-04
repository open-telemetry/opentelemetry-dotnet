namespace Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using OpenTelemetry.Collector.StackExchangeRedis;
    using OpenTelemetry.Exporter.Zipkin;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Sampler;
    using StackExchange.Redis;

    internal class TestRedis
    {
        internal static object Run(string zipkinUri)
        {
            // 1. Configure exporter to export traces to Zipkin
            var exporter = new ZipkinTraceExporter(
                new ZipkinTraceExporterOptions()
                {
                    Endpoint = new Uri(zipkinUri),
                    ServiceName = "tracing-to-zipkin-service",
                },
                Tracing.ExportComponent);
            exporter.Start();

            // 2. Configure 100% sample rate for the purposes of the demo
            ITraceConfig traceConfig = Tracing.TraceConfig;
            ITraceParams currentConfig = traceConfig.ActiveTraceParams;
            var newConfig = currentConfig.ToBuilder()
                .SetSampler(Samplers.AlwaysSample)
                .Build();
            traceConfig.UpdateActiveTraceParams(newConfig);

            // 3. Tracer is global singleton. You can register it via dependency injection if it exists
            // but if not - you can use it as follows:
            var tracer = Tracing.Tracer;

            var collector = new StackExchangeRedisCallsCollector(null, tracer, null, Tracing.ExportComponent);

            // connect to the server
            ConnectionMultiplexer connection = ConnectionMultiplexer.Connect("localhost:6379");
            connection.RegisterProfiler(collector.GetProfilerSessionsFactory());

            // select a database (by default, DB = 0)
            IDatabase db = connection.GetDatabase();


            // 4. Create a scoped span. It will end automatically when using statement ends
            using (var scope = tracer.SpanBuilder("Main").StartScopedSpan())
            {
                Console.WriteLine("About to do a busy work");
                for (int i = 0; i < 10; i++)
                {
                    DoWork(db, i);
                }
            }

            // 5. Gracefully shutdown the exporter so it'll flush queued traces to Zipkin.
            Tracing.ExportComponent.SpanExporter.Dispose();

            return null;
        }

        private static void DoWork(IDatabase db, int i)
        {
            // 6. Get the global singleton Tracer object
            ITracer tracer = Tracing.Tracer;

            // 7. Start another span. If another span was already started, it'll use that span as the parent span.
            // In this example, the main method already started a span, so that'll be the parent span, and this will be
            // a child span.
            using (OpenTelemetry.Common.IScope scope = tracer.SpanBuilder("DoWork").StartScopedSpan())
            {
                // Simulate some work.
                ISpan span = tracer.CurrentSpan;

                try
                {
                    db.StringSet("key", "value " + DateTime.Now.ToLongDateString());

                    Console.WriteLine("Doing busy work");
                    Thread.Sleep(1000);

                    // run a command, in this case a GET
                    RedisValue myVal = db.StringGet("key");

                    Console.WriteLine(myVal);

                }
                catch (ArgumentOutOfRangeException e)
                {
                    // 6. Set status upon error
                    span.Status = Status.Internal.WithDescription(e.ToString());
                }

                // 7. Annotate our span to capture metadata about our operation
                var attributes = new Dictionary<string, IAttributeValue>();
                attributes.Add("use", AttributeValue.StringAttributeValue("demo"));
                span.AddEvent("Invoking DoWork", attributes);
            }
        }
    }
}
