// <copyright file="OtlpHttpTraceExportClient.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Google.Protobuf;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Class for sending OTLP trace export request over HTTP.</summary>
    internal sealed class OtlpHttpTraceExportClient : BaseOtlpHttpExportClient<OtlpCollector.ExportTraceServiceRequest>
    {
        internal const string MediaContentType = "application/x-protobuf";
        private readonly Uri exportTracesUri;

        public OtlpHttpTraceExportClient(OtlpExporterOptions options, HttpClient httpClient = null)
            : base(options, httpClient)
        {
            this.exportTracesUri = this.Options.Endpoint.AppendPathIfNotPresent(OtlpExporterOptions.TracesExportPath);
        }

        protected override HttpRequestMessage CreateHttpRequest(OtlpCollector.ExportTraceServiceRequest exportRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.exportTracesUri);
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
