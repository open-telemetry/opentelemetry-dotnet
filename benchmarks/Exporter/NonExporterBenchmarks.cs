using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace Benchmarks.Exporter
{
    /// <summary>
    /// These benchmarks compare OpenTelemetry without an Exporter vs with a NoOp Exporter
    /// </summary>
    /// <remarks>
    /// To run: .\Benchmarks.exe --filter NonExporterBenchmarks
    /// </remarks>
    [MemoryDiagnoser]
#if !NET462
    [ThreadingDiagnoser]
#endif
    public class NonExporterBenchmarks
    {

        [Params(1, 10)]
        public int NumberOfTraces { get; set; }

        [Params(1, 10)]
        public int NumberOfSpans { get; set; }


        [Benchmark]
        public void NoExporter()
        {
            using var tracerFactory = TracerFactory.Create(builder => builder
                .SetResource(Resources.CreateServiceResource("my-service-name")));

            this.RunTest(tracerFactory);
        }

        [Benchmark]
        public void Exporter_NoOp()
        {
            using var tracerFactory = TracerFactory.Create(builder => builder
                .SetResource(Resources.CreateServiceResource("my-service-name"))
                .AddProcessorPipeline(p => p.SetExporter(new NoOpExporter())));

            this.RunTest(tracerFactory);
        }


        private void RunTest(TracerFactory tracerFactory)
        {
            var tracer = tracerFactory.GetTracer("console-exporter-test");

            for (int iTrace = 0; iTrace < this.NumberOfTraces; iTrace++)
            {
                using (tracer.StartActiveSpan("incoming request", out var span))
                {
                    for (int iSpan = 0; iSpan < this.NumberOfSpans; iSpan++)
                    {
                        span.AddEvent("internal span");
                    }
                }
            }
        }

        private class NoOpExporter : SpanExporter
        {
            public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
            {
                return Task.FromResult(ExportResult.Success);
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
