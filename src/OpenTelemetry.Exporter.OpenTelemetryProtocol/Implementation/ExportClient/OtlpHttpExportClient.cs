// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics.Tracing;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP trace export request over HTTP.</summary>
internal sealed class OtlpHttpExportClient : OtlpExportClient
{
    internal static readonly MediaTypeHeaderValue MediaHeaderValue = new("application/x-protobuf");
    private static readonly ExportClientHttpResponse SuccessExportResponse = new(success: true, deadlineUtc: default, response: null, exception: null);

    internal OtlpHttpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
        : base(options, httpClient, signalPath)
    {
    }

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);
            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            try
            {
                httpResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                if (OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.All))
                {
                    var response = TryGetResponseBody(httpResponse, cancellationToken);
                    OpenTelemetryProtocolExporterEventSource.Log.HttpRequestFailed(this.Endpoint, response, ex);
                }

                return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: httpResponse, ex);
            }

            OpenTelemetryProtocolExporterEventSource.Log.ExportSuccess(this.Endpoint, "Export completed successfully.");
            return SuccessExportResponse;
        }
        catch (HttpRequestException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
            return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: null, exception: ex);
        }
    }

    protected override HttpContent CreateHttpContent(byte[] buffer, int contentLength)
    {
        if (!this.CompressionEnabled)
        {
            return base.CreateHttpContent(buffer, contentLength);
        }

#if NET
        var compressedStream = new PooledBufferStream();
#else
        var compressedStream = new MemoryStream();
#endif

        using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzipStream.Write(buffer, 0, contentLength);
        }

        compressedStream.Position = 0;

        OpenTelemetryProtocolExporterEventSource.Log.CompressedHttpPayload(contentLength, compressedStream.Length);

        var content = new StreamContent(compressedStream);

        content.Headers.ContentType = this.MediaTypeHeader;
        content.Headers.Add("Content-Encoding", "gzip");

        return content;
    }
}
