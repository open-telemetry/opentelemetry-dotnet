// <copyright file="JaegerExporter.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Exporter.Jaeger.Implementation;
    using OpenTelemetry.Trace.Export;

    public class JaegerExporter : IDisposable
    {
        public const string DefaultAgentUdpHost = "localhost";
        public const int DefaultAgentUdpCompactPort = 6831;
        public const int DefaultMaxPacketSize = 65000;

        private const string ExporterName = "JaegerTraceExporter";

        private readonly object @lock = new object();
        private readonly JaegerExporterOptions options;
        private readonly ISpanExporter spanExporter;

        private volatile bool isInitialized = false;
        private JaegerTraceExporterHandler handler;
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerExporter(JaegerExporterOptions options, ISpanExporter spanExporter)
        {
            this.ValidateOptions(options);
            this.InitializeOptions(options);

            this.options = options;
            this.spanExporter = spanExporter;
        }

        public void Start()
        {
            lock (this.@lock)
            {
                if (this.isInitialized)
                {
                    return;
                }

                if (this.spanExporter != null)
                {
                    this.handler = new JaegerTraceExporterHandler(this.options);
                    this.spanExporter.RegisterHandler(ExporterName, this.handler);
                }
            }
        }

        public void Stop()
        {
            if (!this.isInitialized)
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.spanExporter != null)
                {
                    this.spanExporter.UnregisterHandler(ExporterName);
                }
            }

            this.isInitialized = false;
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
                    this.handler.Dispose();
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
