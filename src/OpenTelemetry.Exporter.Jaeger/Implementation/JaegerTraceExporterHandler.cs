// <copyright file="JaegerTraceExporterHandler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using Thrift.Protocols;

    public class JaegerTraceExporterHandler : IHandler, IDisposable
    {
        private readonly IJaegerUdpBatcher jaegerAgentUdpBatcher;
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerTraceExporterHandler(JaegerExporterOptions options)
            : this(new JaegerUdpBatcher(options))
        {
        }

        public JaegerTraceExporterHandler(IJaegerUdpBatcher jaegerAgentUdpBatcher)
        {
            this.jaegerAgentUdpBatcher = jaegerAgentUdpBatcher;
        }

        public async Task ExportAsync(IEnumerable<Span> otelSpanList)
        {
            var jaegerspans = otelSpanList.Select(sdl => sdl.ToJaegerSpan());

            foreach (var s in jaegerspans)
            {
                await this.jaegerAgentUdpBatcher.AppendAsync(s, CancellationToken.None);
            }

            await this.jaegerAgentUdpBatcher.FlushAsync(CancellationToken.None);
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
    }
}
