// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
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

    protected override string? ContentEncodingHeader => "gzip";

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
                OpenTelemetryProtocolExporterEventSource.Log.HttpRequestFailed(this.Endpoint, ex);
                return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: httpResponse, ex);
            }

            return SuccessExportResponse;
        }
        catch (HttpRequestException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
            return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: null, exception: ex);
        }
    }

    protected override byte[] Compress(byte[] data, int contentLength)
    {
        using var compressedStream = new MemoryStream();
        using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
        {
#if NET462 || NETSTANDARD2_0
            gzipStream.Write(data.ToArray(), 0, data.Length);
#else
            gzipStream.Write(data);
#endif
        }

        return compressedStream.ToArray();
    }
}
