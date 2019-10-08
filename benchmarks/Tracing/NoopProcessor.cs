using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace Benchmarks.Tracing
{
    internal class NoopProcessor : SpanProcessor
    {
        public NoopProcessor() : base(new NoopExporter())
        {
        }

        public override void OnStart(Span span)
        {
        }

        public override void OnEnd(Span span)
        {
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private class NoopExporter : SpanExporter
        {
            public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
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
