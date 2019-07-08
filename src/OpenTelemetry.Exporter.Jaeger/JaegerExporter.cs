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
    using OpenTelemetry.Exporter.Jaeger.Implimentation;
    using OpenTelemetry.Trace.Export;

    public class JaegerExporter : IDisposable
    {
        private const string ExporterName = "JaegerTraceExporter";

        private readonly object lck = new object();
        private readonly JaegerExporterOptions options;
        private readonly IExportComponent exportComponent;

        private bool isInitialized = false;
        private JaegerTraceExporterHandler handler;
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerExporter(JaegerExporterOptions options, IExportComponent exportComponent)
        {
            this.options = options;
            this.exportComponent = exportComponent;
        }

        public void Start()
        {
            lock (this.lck)
            {
                if (this.isInitialized)
                {
                    return;
                }

                if (this.exportComponent != null)
                {
                    this.handler = new JaegerTraceExporterHandler(this.options);
                    this.exportComponent.SpanExporter.RegisterHandler(ExporterName, this.handler);
                }
            }
        }

        public void Stop()
        {
            if (!this.isInitialized)
            {
                return;
            }

            lock (this.lck)
            {
                if (this.exportComponent != null)
                {
                    this.exportComponent.SpanExporter.UnregisterHandler(ExporterName);
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
    }
}
