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

namespace OpenTelemetry.Exporter.Jaeger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Exporter.Jaeger.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    public class JaegerTraceExporter : SpanExporter, IDisposable
    {
        public const string DefaultAgentUdpHost = "localhost";
        public const int DefaultAgentUdpCompactPort = 6831;
        public const int DefaultMaxPacketSize = 65000;

        private readonly IJaegerUdpBatcher jaegerAgentUdpBatcher;
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerTraceExporter(JaegerExporterOptions options)
        {
            this.ValidateOptions(options);
            this.InitializeOptions(options);
        }

        public JaegerTraceExporter(IJaegerUdpBatcher jaegerAgentUdpBatcher)
        {
            this.jaegerAgentUdpBatcher = jaegerAgentUdpBatcher;
        }

        public override async Task<ExportResult> ExportAsync(IEnumerable<Span> otelSpanList, CancellationToken cancellationToken)
        {
            var jaegerspans = otelSpanList.Select(sdl => sdl.ToJaegerSpan());

            foreach (var s in jaegerspans)
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
                throw new ArgumentException("ServiceName", "Service Name is required.");
            }
        }

        private void InitializeOptions(JaegerExporterOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.AgentHost))
            {
                options.AgentHost = DefaultAgentUdpHost;
            }

            if (!options.AgentPort.HasValue)
            {
                options.AgentPort = DefaultAgentUdpCompactPort;
            }

            if (!options.MaxPacketSize.HasValue)
            {
                options.MaxPacketSize = DefaultMaxPacketSize;
            }
        }
    }
}
