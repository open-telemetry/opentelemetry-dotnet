// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
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

    internal OtlpHttpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath, OtlpSignalType? signalType = null)
        : base(options, httpClient, signalPath, signalType)
    {
    }

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);

#if NETFRAMEWORK
            if (this.CompressionEnabled)
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

            this.TryReportPartialSuccess(httpResponse, deadlineUtc, cancellationToken);

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

    protected override HttpContent CreateHttpContent(byte[] buffer, int contentLength) =>
        this.CompressionEnabled
            ? new GZipHttpContent(buffer, contentLength, this.MediaTypeHeader)
            : base.CreateHttpContent(buffer, contentLength);

    private static bool IsProtobufResponse(HttpContentHeaders headers)
    {
        // A response without a media type is assumed to be the protobuf the request was sent as.
        var mediaType = headers.ContentType?.MediaType;
        return mediaType is null || string.Equals(mediaType, MediaHeaderValue.MediaType, StringComparison.OrdinalIgnoreCase);
    }

    private void TryReportPartialSuccess(HttpResponseMessage httpResponse, DateTime deadlineUtc, CancellationToken cancellationToken)
    {
        if (!IsPartialSuccessReportingEnabled()
            || httpResponse.Content is not { } content
            || content.Headers.ContentLength == 0
            || !IsProtobufResponse(content.Headers))
        {
            return;
        }

        var client = this;

        // A response without a declared size that is still too large is not read beyond the limit.
        TryReadResponseBody(
            httpResponse,
            this.MaxResponseSizeBytes,
            deadlineUtc,
            static (byte[] buffer, int length, bool truncated, ref OtlpHttpExportClient client) =>
            {
                if (truncated)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.ResponseDiscardedDueToSizeLimit(client.Endpoint, null, client.MaxResponseSizeBytes);
                }
                else
                {
                    client.ReportPartialSuccess(buffer, 0, length);
                }
            },
            ref client,
            cancellationToken);
    }
}
