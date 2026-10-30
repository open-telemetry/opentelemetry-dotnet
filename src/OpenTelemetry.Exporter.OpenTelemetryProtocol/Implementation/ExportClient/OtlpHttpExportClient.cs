// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
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
    protected override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, OtlpExportCompression compression, DateTime deadlineUtc, CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength, compression);

#if NETFRAMEWORK
            if (compression != OtlpExportCompression.None)
            {
                // The length of the compressed content is not known up front, so unless the request
                // is sent chunked HttpClientHandler buffers the entire compressed body in memory first.
                httpRequest.Headers.TransferEncodingChunked = true;
            }
#endif

            using var httpResponse = this.SendHttpRequest(httpRequest, deadlineUtc, cancellationToken);

            if (this.IsResponseTooLarge(httpResponse, out var responseTooLarge))
            {
                // Requests with responses that are too large must not be retried
                return new ExportClientHttpResponse(
                    success: false,
                    deadlineUtc: deadlineUtc,
                    response: null,
                    exception: responseTooLarge);
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                var ex = CreateStatusCodeException(httpResponse);

                if (OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.All))
                {
                    var response = TryGetResponseBody(httpResponse, Math.Min(this.MaxResponseSizeBytes, MaxLoggedResponseSizeBytes), deadlineUtc, cancellationToken);
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
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            // Handle TaskCanceledException caused by TimeoutException.
            OpenTelemetryProtocolExporterEventSource.Log.RequestTimedOut(this.Endpoint, ex);
            return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: null, exception: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Handle unexpected cancellation.
            OpenTelemetryProtocolExporterEventSource.Log.OperationUnexpectedlyCanceled(this.Endpoint, ex);
            return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: null, exception: ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Return a response with no status code. OtlpRetry.IsRetryable treats a missing
            // status code as retryable, preserving any blob written to persistent storage.
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
            return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: null, exception: ex);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Receivers that do not support a Content-Encoding should return 415, but may return 400.
    /// </remarks>
    protected override bool IsCompressionRejected(ExportClientResponse response) =>
        response is ExportClientHttpResponse { StatusCode: HttpStatusCode.UnsupportedMediaType or HttpStatusCode.BadRequest };

    protected override HttpContent CreateHttpContent(byte[] buffer, int contentLength, OtlpExportCompression compression) =>
        compression switch
        {
            OtlpExportCompression.GZip => new GZipHttpContent(buffer, contentLength, this.MediaTypeHeader),
#if NET11_0_OR_GREATER
            OtlpExportCompression.Zstd => new ZstdHttpContent(buffer, contentLength, this.MediaTypeHeader, this.GetZstdCompressor()),
#endif
            _ => base.CreateHttpContent(buffer, contentLength, compression),
        };
}
