// <copyright file="OtlpHttpTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP) over HTTP.
    /// </summary>
    public class OtlpHttpTraceExporter : BaseOtlpHttpExporter<Activity>
    {
        internal const string MediaContentType = "application/x-protobuf";

        internal OtlpHttpTraceExporter(OtlpExporterOptions options, IHttpHandler httpHandler = null)
            : base(options, httpHandler)
        {
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            // Prevents the exporter's gRPC and HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            var exportRequest = new OtlpCollector.ExportTraceServiceRequest();
            exportRequest.AddBatch(this.ProcessResource, activityBatch);

            try
            {
                using var request = this.CreateHttpRequest(exportRequest);

                using var response = this.HttpHandler.Send(request);

                response?.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(ex);

                return ExportResult.Failure;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);

                return ExportResult.Failure;
            }
            finally
            {
                exportRequest.Return();
            }

            return ExportResult.Success;
        }

        private HttpRequestMessage CreateHttpRequest(OtlpCollector.ExportTraceServiceRequest exportRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.Options.Endpoint);
            foreach (var header in this.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            var content = Array.Empty<byte>();
            using (var stream = new MemoryStream())
            {
                exportRequest.WriteTo(stream);
                content = stream.ToArray();
            }

            var binaryContent = new ByteArrayContent(content);
            binaryContent.Headers.ContentType = new MediaTypeHeaderValue(MediaContentType);
            request.Content = binaryContent;

            return request;
        }
    }
}
