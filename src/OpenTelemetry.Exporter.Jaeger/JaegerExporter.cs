// <copyright file="JaegerExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerExporter : ActivityExporter, IDisposable
    {
        private bool libraryResourceApplied = false;
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerExporter(JaegerExporterOptions options)
        {
            this.JaegerAgentUdpBatcher = new JaegerUdpBatcher(options);
        }

        internal JaegerUdpBatcher JaegerAgentUdpBatcher { get; }

        public override async Task<ExportResult> ExportAsync(IEnumerable<Activity> activityBatch, CancellationToken cancellationToken)
        {
            if (!this.libraryResourceApplied && activityBatch.Count() > 0)
            {
                var libraryResource = activityBatch.First().GetResource();

                this.ApplyLibraryResource(libraryResource ?? Resource.Empty);

                this.libraryResourceApplied = true;
            }

            await this.JaegerAgentUdpBatcher.AppendBatchAsync(activityBatch, cancellationToken).ConfigureAwait(false);

            // TODO jaeger status to ExportResult
            return ExportResult.Success;
        }

        public override async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            await this.JaegerAgentUdpBatcher.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);
        }

        internal void ApplyLibraryResource(Resource libraryResource)
        {
            if (libraryResource is null)
            {
                throw new ArgumentNullException(nameof(libraryResource));
            }

            var process = this.JaegerAgentUdpBatcher.Process;

            string serviceName = null;
            string serviceNamespace = null;
            foreach (var label in libraryResource.Attributes)
            {
                string key = label.Key;

                if (label.Value is string strVal)
                {
                    switch (key)
                    {
                        case Resource.ServiceNameKey:
                            serviceName = strVal;
                            continue;
                        case Resource.ServiceNamespaceKey:
                            serviceNamespace = strVal;
                            continue;
                        case Resource.LibraryNameKey:
                        case Resource.LibraryVersionKey:
                            continue;
                    }
                }

                if (process.Tags == null)
                {
                    process.Tags = new Dictionary<string, JaegerTag>();
                }

                process.Tags[label.Key] = label.ToJaegerTag();
            }

            if (serviceName != null)
            {
                process.ServiceName = serviceNamespace != null
                    ? serviceNamespace + "." + serviceName
                    : serviceName;
            }

            if (string.IsNullOrEmpty(process.ServiceName))
            {
                process.ServiceName = JaegerExporterOptions.DefaultServiceName;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.JaegerAgentUdpBatcher.Dispose();
                }

                this.disposedValue = true;
            }
        }
    }
}
