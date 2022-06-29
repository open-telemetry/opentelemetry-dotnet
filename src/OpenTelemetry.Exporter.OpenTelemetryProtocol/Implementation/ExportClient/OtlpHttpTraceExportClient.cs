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

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Threading;
#endif
using System.Threading.Tasks;
using Google.Protobuf;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Class for sending OTLP trace export request over HTTP.</summary>
    internal sealed class OtlpHttpTraceExportClient : BaseOtlpHttpExportClient<OtlpCollector.ExportTraceServiceRequest>
    {
        internal const string MediaContentType = "application/x-protobuf";
        private const string TracesExportPath = "v1/traces";

        public OtlpHttpTraceExportClient(OtlpExporterOptions options, HttpClient httpClient)
            : base(options, httpClient, TracesExportPath)
        {
        }

        protected override HttpContent CreateHttpContent(OtlpCollector.ExportTraceServiceRequest exportRequest)
        {
            return new ExportRequestContent(exportRequest);
        }

        internal sealed class ExportRequestContent : HttpContent
        {
            private static readonly MediaTypeHeaderValue ProtobufMediaTypeHeader = new(MediaContentType);

            private readonly OtlpCollector.ExportTraceServiceRequest exportRequest;

            public ExportRequestContent(OtlpCollector.ExportTraceServiceRequest exportRequest)
            {
                this.exportRequest = exportRequest;
                this.Headers.ContentType = ProtobufMediaTypeHeader;
            }

#if NET6_0_OR_GREATER
            protected override void SerializeToStream(Stream stream, TransportContext context, CancellationToken cancellationToken)
            {
                this.SerializeToStreamInternal(stream);
            }
#endif

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                this.SerializeToStreamInternal(stream);
                return Task.CompletedTask;
            }

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream.
                length = -1;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SerializeToStreamInternal(Stream stream)
            {
                this.exportRequest.WriteTo(stream);
            }
        }
    }
}
