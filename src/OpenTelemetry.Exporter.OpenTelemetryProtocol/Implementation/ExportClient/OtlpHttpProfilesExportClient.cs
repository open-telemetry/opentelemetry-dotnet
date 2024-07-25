// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using OtlpCollector = OpenTelemetry.Proto.Collector.Profiles.V1Experimental;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP log export request over HTTP.</summary>
internal sealed class OtlpHttpProfilesExportClient : BaseOtlpHttpExportClient<OtlpCollector.ExportProfilesServiceRequest>
{
    internal const string MediaContentType = "application/x-protobuf";
    private const string LogsExportPath = "v1experimental/profiles";

    public OtlpHttpProfilesExportClient(OtlpExporterOptions options, HttpClient httpClient)
        : base(options, httpClient, LogsExportPath)
    {
    }

    protected override HttpContent CreateHttpContent(OtlpCollector.ExportProfilesServiceRequest exportRequest)
    {
        return new ExportRequestContent(exportRequest);
    }

    internal sealed class ExportRequestContent : HttpContent
    {
        private static readonly MediaTypeHeaderValue ProtobufMediaTypeHeader = new(MediaContentType);

        private readonly OtlpCollector.ExportProfilesServiceRequest exportRequest;

        public ExportRequestContent(OtlpCollector.ExportProfilesServiceRequest exportRequest)
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
