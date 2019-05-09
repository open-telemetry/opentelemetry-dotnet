// <copyright file="OcagentExporter.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Exporter.Ocagent
{
    using Grpc.Core;

    using OpenCensus.Exporter.Ocagent.Implementation;
    using OpenCensus.Trace.Export;

    /// <summary>
    /// Exporter of Open Census traces to the Ocagent or LocalForwarder.
    /// </summary>
    public class OcagentExporter
    {
        private const string TraceExporterName = "OcagentTraceExporter";

        private readonly IExportComponent exportComponent;

        private readonly object lck = new object();

        private readonly string agentEndpoint;
        private readonly string hostName;
        private readonly string serviceName;
        private TraceExporterHandler handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="OcagentExporter"/> class.
        /// This exporter allows to send Open Census data to OpenCensus service or LocalForwarder.
        /// </summary>
        /// <param name="exportComponent">Exporter to get traces from.</param>
        /// <param name="agentEndpoint">Agent endpoint in the host:port format.</param>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="serviceName">Name of the application.</param>
        public OcagentExporter(
            IExportComponent exportComponent,
            string agentEndpoint,
            string hostName,
            string serviceName)
        {
            this.exportComponent = exportComponent;
            this.agentEndpoint = agentEndpoint;
            this.hostName = hostName;
            this.serviceName = serviceName;
        }

        /// <summary>
        /// Start exporter.
        /// </summary>
        public void Start()
        {
            lock (this.lck)
            {
                if (this.handler != null)
                {
                    return;
                }

                this.handler = new TraceExporterHandler(
                    this.agentEndpoint,
                    this.hostName,
                    this.serviceName,
                    ChannelCredentials.Insecure);

                this.exportComponent.SpanExporter.RegisterHandler(TraceExporterName, this.handler);
            }
        }

        /// <summary>
        /// Stop exporter.
        /// </summary>
        public void Stop()
        {
            lock (this.lck)
            {
                if (this.handler == null)
                {
                    return;
                }

                this.exportComponent.SpanExporter.UnregisterHandler(TraceExporterName);
                this.handler.Dispose();
                this.handler = null;
            }
        }
    }
}
