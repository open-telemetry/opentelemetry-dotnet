// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP trace export request over HTTP.</summary>
internal sealed class OtlpHttpTraceExportClient : BaseOtlpHttpExportClient<OtlpCollector.ExportTraceServiceRequest>
{
    internal const string MediaContentType = "application/x-protobuf";
    private const string TracesExportPath = "v1/traces";

    public OtlpHttpTraceExportClient(OtlpExporterOptions options, HttpClient httpClient)
        : base(options, ModifyHttpClient(options, httpClient), TracesExportPath)
    {
    }

    protected override HttpContent CreateHttpContent(OtlpCollector.ExportTraceServiceRequest exportRequest)
    {
        return new ExportRequestContent(exportRequest);
    }

    private static HttpClient ModifyHttpClient(OtlpExporterOptions options, HttpClient httpClient)
    {
        // Create a new handler using the existing method that configures mTLS
        var handler = options.CreateDefaultHttpMessageHandler();

        // Create a new HttpClient with the mTLS-enabled handler
        var newHttpClient = new HttpClient(handler, disposeHandler: true);

        // Copy existing headers from the original HttpClient
        foreach (var header in httpClient.DefaultRequestHeaders)
        {
            newHttpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        // Copy other properties, such as timeout, if needed
        newHttpClient.Timeout = httpClient.Timeout;

        return newHttpClient;
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

#if NET
        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            this.SerializeToStreamInternal(stream);
        }
#endif

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
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
