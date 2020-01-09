// <copyright file="JaegerTraceExporter.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerTraceExporter : SpanExporter, IDisposable
    {
        private readonly IJaegerUdpBatcher jaegerAgentUdpBatcher;
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerTraceExporter(JaegerExporterOptions options)
        {
            this.ValidateOptions(options);
            this.jaegerAgentUdpBatcher = new JaegerUdpBatcher(options);
        }

        public JaegerTraceExporter(IJaegerUdpBatcher jaegerAgentUdpBatcher)
        {
            this.jaegerAgentUdpBatcher = jaegerAgentUdpBatcher;
        }

        public override async Task<ExportResult> ExportAsync(IEnumerable<SpanData> otelSpanList, CancellationToken cancellationToken)
        {
            var jaegerSpans = otelSpanList.Select(sdl => sdl.ToJaegerSpan());

            foreach (var s in jaegerSpans)
            {
                // avoid cancelling here: this is no return point: if we reached this point
                // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
                await this.jaegerAgentUdpBatcher.AppendAsync(s, CancellationToken.None);
            }

            // TODO jaeger status to ExportResult
            return ExportResult.Success;
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return this.jaegerAgentUdpBatcher.FlushAsync(cancellationToken);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.jaegerAgentUdpBatcher.Dispose();
                }

                this.disposedValue = true;
            }
        }

        private void ValidateOptions(JaegerExporterOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ServiceName))
            {
                throw new ArgumentException("Service Name is required", nameof(options.ServiceName));
            }
        }
    }
}
