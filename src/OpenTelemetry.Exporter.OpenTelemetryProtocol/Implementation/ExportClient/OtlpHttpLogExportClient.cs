// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP log export request over HTTP.</summary>
internal sealed class OtlpHttpLogExportClient : BaseOtlpHttpExportClient<OtlpCollector.ExportLogsServiceRequest>
{
    internal const string MediaContentType = "application/x-protobuf";
    private const string LogsExportPath = "v1/logs";

    public OtlpHttpLogExportClient(OtlpExporterOptionsBase options, HttpClient httpClient)
        : base(options, httpClient, LogsExportPath)
    {
    }

    protected override HttpContent CreateHttpContent(OtlpCollector.ExportLogsServiceRequest exportRequest)
    {
        return new ExportRequestContent(exportRequest);
    }

    internal sealed class ExportRequestContent : HttpContent
    {
        private static readonly MediaTypeHeaderValue ProtobufMediaTypeHeader = new(MediaContentType);

        private readonly OtlpCollector.ExportLogsServiceRequest exportRequest;

        public ExportRequestContent(OtlpCollector.ExportLogsServiceRequest exportRequest)
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
