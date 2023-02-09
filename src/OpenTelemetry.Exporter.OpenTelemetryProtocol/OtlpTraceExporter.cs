// <copyright file="OtlpTraceExporter.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Extensions.PersistentStorage.Abstractions;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpTraceExporter : BaseExporter<Activity>
    {
        private readonly SdkLimitOptions sdkLimitOptions;
        private readonly IExportClient<OtlpCollector.ExportTraceServiceRequest> exportClient;

        private OtlpResource.Resource processResource;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the export.</param>
        public OtlpTraceExporter(OtlpExporterOptions options)
            : this(options, new(), null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
        /// </summary>
        /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
        /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
        /// <param name="exportClient">Client used for sending export request.</param>
        /// <param name="persistentStorageFactory"><see cref="PersistentBlobProvider"/>.</param>
        internal OtlpTraceExporter(
            OtlpExporterOptions exporterOptions,
            SdkLimitOptions sdkLimitOptions,
            IExportClient<OtlpCollector.ExportTraceServiceRequest> exportClient = null,
            Func<PersistentBlobProvider> persistentStorageFactory = null)
        {
            Debug.Assert(exporterOptions != null, "exporterOptions was null");
            Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");

            this.sdkLimitOptions = sdkLimitOptions;

            if (exportClient != null)
            {
                this.exportClient = exportClient;
            }
            else
            {
                this.exportClient = exporterOptions.GetTraceExportClient(persistentStorageFactory());
            }
        }

        internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            var request = new OtlpCollector.ExportTraceServiceRequest();

            try
            {
                request.AddBatch(this.sdkLimitOptions, this.ProcessResource, activityBatch);

                if (!this.exportClient.SendExportRequest(request))
                {
                    return ExportResult.Failure;
                }
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
                return ExportResult.Failure;
            }
            finally
            {
                request.Return();
            }

            return ExportResult.Success;
        }

        /// <inheritdoc />
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            return this.exportClient?.Shutdown(timeoutMilliseconds) ?? true;
        }
    }
}
