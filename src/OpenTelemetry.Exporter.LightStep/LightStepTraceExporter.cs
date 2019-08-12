// <copyright file="LightStepTraceExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.LightStep
{
    using System;
    using System.Net.Http;
    using OpenTelemetry.Exporter.LightStep.Implementation;
    using OpenTelemetry.Trace.Export;

    public class LightStepTraceExporter
    {
        private const string ExporterName = "LightStepExporter";
        private readonly LightStepTraceExporterOptions options;
        private readonly ISpanExporter exporter;
        private readonly object lck = new object();
        private readonly HttpClient httpClient;
        private TraceExporterHandler handler;

        public LightStepTraceExporter(LightStepTraceExporterOptions options, ISpanExporter exporter, HttpClient client = null)
        {
            this.options = options;
            this.exporter = exporter;
            this.httpClient = client;
        }

        public void Start()
        {
            lock (this.lck)
            {
                if (this.handler != null)
                {
                    return;
                }

                this.handler = new TraceExporterHandler(this.options, this.httpClient);

                this.exporter.RegisterHandler(ExporterName, this.handler);
            }
        }

        public void Stop()
        {
            lock (this.lck)
            {
                if (this.handler == null)
                {
                    return;
                }

                this.exporter.UnregisterHandler(ExporterName);
                this.handler = null;
            }
        }
    }
}
