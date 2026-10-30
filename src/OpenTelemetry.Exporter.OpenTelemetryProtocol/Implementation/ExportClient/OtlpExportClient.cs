// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal abstract class OtlpExportClient : IExportClient
{
    /// <summary>
    /// The maximum size, in bytes, of the body of an unsuccessful
    /// response to include in the diagnostic log (4 KiB).
    /// </summary>
    internal const int MaxLoggedResponseSizeBytes = 4 * 1024;

    /// <summary>
    /// The number of consecutive requests that are rejected when sent with Zstandard,
    /// and then fail again when sent with GZip, after which Zstandard is no longer used.
    /// </summary>
    private const int MaxConsecutiveZstdFallbackFailures = 3;

    private static readonly Version Http2RequestVersion = new(2, 0);
    private static readonly TimeSpan MaxDeadlineDelay = TimeSpan.FromMilliseconds(int.MaxValue);

#if NET
    // See: https://github.com/dotnet/runtime/blob/280f2a0c60ce0378b8db49adc0eecc463d00fe5d/src/libraries/System.Net.Http/src/System/Net/Http/HttpClientHandler.AnyMobile.cs#L767
    [System.Runtime.Versioning.UnsupportedOSPlatformGuard("android")]
    [System.Runtime.Versioning.UnsupportedOSPlatformGuard("browser")]
    [System.Runtime.Versioning.UnsupportedOSPlatformGuard("ios")]
    [System.Runtime.Versioning.UnsupportedOSPlatformGuard("tvos")]
    private static readonly bool SynchronousSendSupportedByCurrentPlatform =
        !OperatingSystem.IsAndroid() &&
        !OperatingSystem.IsIOS() &&
        !OperatingSystem.IsTvOS() &&
        !OperatingSystem.IsBrowser();
#endif

    private readonly OtlpExportCompression compression;

#if NET11_0_OR_GREATER
    private readonly ZstdCompressor? zstdCompressor;
#endif

    private int zstdRejected;
    private int zstdFallbackFailures;

    protected OtlpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(httpClient);
        Guard.ThrowIfNull(signalPath);

        Uri exporterEndpoint;
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        if (options.Protocol == OtlpExportProtocol.Grpc)
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
        {
            exporterEndpoint = options.Endpoint.AppendPathIfNotPresent(signalPath);
        }
        else
        {
            exporterEndpoint = options.AppendSignalPathToEndpoint
                ? options.Endpoint.AppendPathIfNotPresent(signalPath)
                : options.Endpoint;
        }

        this.Endpoint = new UriBuilder(exporterEndpoint).Uri;
        this.Headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
        this.HttpClient = httpClient;
        this.compression = options.Compression;

        if (this.compression == OtlpExportCompression.Zstd)
        {
#if NET11_0_OR_GREATER
            if (ZstdCompressor.IsSupported)
            {
                this.zstdCompressor = new ZstdCompressor();
            }
            else
#endif
            {
                OpenTelemetryProtocolExporterEventSource.Log.ZstdCompressionNotSupported();
                this.compression = OtlpExportCompression.GZip;
            }
        }

        this.MaxResponseSizeBytes = options.MaxResponseSizeBytes;
    }

    internal HttpClient HttpClient { get; }

    internal Uri Endpoint { get; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the compression the next request will be sent with.
    /// </summary>
    /// <remarks>
    /// This is <see cref="OtlpExportCompression.GZip"/> if Zstandard compression was configured,
    /// but the receiver rejected a request using it and then accepted it using GZip, or if
    /// <see cref="MaxConsecutiveZstdFallbackFailures"/> requests in a row were rejected using it
    /// and then failed again using GZip.
    /// </remarks>
    internal OtlpExportCompression Compression
        => this.compression == OtlpExportCompression.Zstd && Volatile.Read(ref this.zstdRejected) != 0
            ? OtlpExportCompression.GZip
            : this.compression;

    /// <summary>
    /// Gets the maximum size, in bytes, of a response the exporter will accept.
    /// </summary>
    internal int MaxResponseSizeBytes { get; }

    internal abstract MediaTypeHeaderValue MediaTypeHeader { get; }

    internal virtual long HeaderBytesSize => 0;

    internal virtual bool RequireHttp2 => false;

    internal virtual HttpCompletionOption CompletionOption => HttpCompletionOption.ResponseHeadersRead;

    /// <inheritdoc/>
    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        var compression = this.Compression;
        var response = this.SendExportRequest(buffer, contentLength, compression, deadlineUtc, cancellationToken);

        if (compression == OtlpExportCompression.Zstd && this.IsCompressionRejected(response))
        {
            // Receivers are not required to support Zstandard, so a request they reject is sent again
            // once with GZip so that it is not dropped solely because of the compression used.
            // See https://github.com/open-telemetry/opentelemetry-specification/pull/5321.
            OpenTelemetryProtocolExporterEventSource.Log.ZstdCompressionRejected(this.Endpoint);

            response = this.SendExportRequest(buffer, contentLength, OtlpExportCompression.GZip, deadlineUtc, cancellationToken);

            if (response.Success)
            {
                // Only stop using Zstandard immediately if GZip succeeded, as otherwise the
                // rejection was most likely caused by something other than the compression used.
                Volatile.Write(ref this.zstdRejected, 1);
            }
            else if (Interlocked.Increment(ref this.zstdFallbackFailures) >= MaxConsecutiveZstdFallbackFailures)
            {
                // The receiver keeps rejecting requests whatever their compression, such as when
                // it responds with 400 to a payload it does not accept. Stop sending each
                // request twice, which doubles the bandwidth and CPU used, for no benefit.
                Volatile.Write(ref this.zstdRejected, 1);
            }
        }
        else if (compression == OtlpExportCompression.Zstd)
        {
            // The receiver did not reject the compression, so the failures were not consecutive.
            Volatile.Write(ref this.zstdFallbackFailures, 0);
        }

        return response;
    }

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();

#if NET11_0_OR_GREATER
        this.zstdCompressor?.Shutdown();
#endif

        return true;
    }

    /// <summary>
    /// Gets how long is left until a deadline, for use with
    /// <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>.
    /// </summary>
    /// <param name="deadlineUtc">
    /// The deadline time in UTC, or <see cref="DateTime.MaxValue"/> if there is no deadline.
    /// </param>
    /// <returns>
    /// The time left until the deadline, which is zero or negative if it has passed and at most
    /// <see cref="int.MaxValue"/> milliseconds, or <see langword="null"/> if there is no deadline.
    /// </returns>
    internal static TimeSpan? GetTimeUntilDeadline(DateTime deadlineUtc)
    {
        if (deadlineUtc == DateTime.MaxValue)
        {
            return null;
        }

        var remaining = deadlineUtc - DateTime.UtcNow;

        // CancelAfter() does not accept a delay of more than int.MaxValue milliseconds (about 24.8 days),
        // so a deadline further away than that is reached sooner rather than not at all.
        return remaining > MaxDeadlineDelay ? MaxDeadlineDelay : remaining;
    }

    protected internal static string? TryGetResponseBody(HttpResponseMessage? httpResponse, int maxResponseSizeBytes, CancellationToken cancellationToken) =>
        HttpClientHelpers.TryGetResponseBodyAsString(httpResponse, maxResponseSizeBytes, cancellationToken);

    /// <summary>
    /// Reads the body of a failed response so that it can be logged, giving up
    /// once <paramref name="deadlineUtc"/> has passed.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpClient.Timeout"/> no longer applies once the response headers
    /// have been received, so the export deadline is used to bound how long a server
    /// that announces a body but does not send it can hold the exporting thread.
    /// </remarks>
    /// <param name="httpResponse">The response to read the body of.</param>
    /// <param name="maxResponseSizeBytes">The maximum number of bytes to read.</param>
    /// <param name="deadlineUtc">The deadline time in UTC for the export request to finish.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>The response body, or <see langword="null"/> if it could not be read in time.</returns>
    protected internal static string? TryGetResponseBody(
        HttpResponseMessage? httpResponse,
        int maxResponseSizeBytes,
        DateTime deadlineUtc,
        CancellationToken cancellationToken)
    {
        if (GetTimeUntilDeadline(deadlineUtc) is not { } remaining)
        {
            // There is no deadline, so only the caller's token applies.
            return TryGetResponseBody(httpResponse, maxResponseSizeBytes, cancellationToken);
        }

        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        deadlineCts.CancelAfter(remaining);

        return TryGetResponseBody(httpResponse, maxResponseSizeBytes, deadlineCts.Token);
    }

    /// <summary>
    /// Creates the exception that <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
    /// would throw for a response whose status code does not indicate success.
    /// </summary>
    /// <remarks>
    /// On .NET Framework <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> disposes
    /// of the response content before throwing, which would prevent the body from being read
    /// for the exporter's diagnostic log, so the exception is created without calling it.
    /// </remarks>
    /// <param name="httpResponse">The response whose status code does not indicate success.</param>
    /// <returns>The <see cref="HttpRequestException"/> describing the failure.</returns>
    protected static HttpRequestException CreateStatusCodeException(HttpResponseMessage httpResponse)
    {
        var message = $"Response status code does not indicate success: {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}).";

#if NET
        return new HttpRequestException(message, inner: null, httpResponse.StatusCode);
#else
        return new HttpRequestException(message);
#endif
    }

    /// <summary>
    /// Determines whether a response declares more content than <see
    /// cref="MaxResponseSizeBytes"/> (plus any <see cref="HeaderBytesSize"/>)
    /// allows, recording that it is being discarded.
    /// </summary>
    /// <param name="httpResponse">The response to check.</param>
    /// <param name="exception">The failure to report when the response is too large.</param>
    /// <returns><see langword="true"/> when the response must be discarded.</returns>
    protected bool IsResponseTooLarge(
        HttpResponseMessage httpResponse,
#if NET
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
#endif
        out ResponseSizeLimitExceededException? exception)
    {
        var contentLength = httpResponse.Content?.Headers.ContentLength;
        var maxContentLength = this.MaxResponseSizeBytes + this.HeaderBytesSize;

        if (contentLength > maxContentLength)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ResponseDiscardedDueToSizeLimit(this.Endpoint, contentLength, this.MaxResponseSizeBytes);
            exception = new ResponseSizeLimitExceededException(contentLength, this.MaxResponseSizeBytes);
            return true;
        }

        exception = null;
        return false;
    }

    /// <summary>
    /// Sends an export request using the specified compression.
    /// </summary>
    /// <param name="buffer">The request body.</param>
    /// <param name="contentLength">The number of bytes within <paramref name="buffer"/> that make up the message.</param>
    /// <param name="compression">The compression to apply to the request body.</param>
    /// <param name="deadlineUtc">The deadline time in UTC for the export request to finish.</param>
    /// <param name="cancellationToken">An optional token for canceling the call.</param>
    /// <returns><see cref="ExportClientResponse"/>.</returns>
    protected abstract ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, OtlpExportCompression compression, DateTime deadlineUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a failed response indicates that the receiver does not
    /// support the compression the request was sent with.
    /// </summary>
    /// <param name="response">The response to inspect.</param>
    /// <returns><see langword="true"/> if the receiver rejected the request's compression.</returns>
    protected abstract bool IsCompressionRejected(ExportClientResponse response);

    protected HttpRequestMessage CreateHttpRequest(byte[] buffer, int contentLength, OtlpExportCompression compression)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint);

        if (this.RequireHttp2)
        {
            request.Version = Http2RequestVersion;

#if NET
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif
        }

        foreach (var header in this.Headers)
        {
            try
            {
                request.Headers.Add(header.Key, header.Value);
            }
            catch (FormatException)
            {
                throw new FormatException($"The value configured for the '{header.Key}' header is not a valid HTTP header value.");
            }
        }

        request.Content = this.CreateHttpContent(buffer, contentLength, compression);

        return request;
    }

    /// <summary>
    /// Creates the <see cref="HttpContent"/> for a request. Override in subclasses to
    /// customise content creation (e.g. to apply compression).
    /// </summary>
    /// <param name="buffer">The serialized protobuf payload buffer.</param>
    /// <param name="contentLength">The number of bytes within <paramref name="buffer"/> that make up the message.</param>
    /// <param name="compression">The compression to apply to the payload.</param>
    /// <returns>An <see cref="HttpContent"/> representing the export payload.</returns>
    protected virtual HttpContent CreateHttpContent(byte[] buffer, int contentLength, OtlpExportCompression compression)
    {
#if NET
        var content = new ReadOnlyMemoryContent(buffer.AsMemory(0, contentLength));
#else
        var content = new ByteArrayContent(buffer, 0, contentLength);
#endif
        content.Headers.ContentType = this.MediaTypeHeader;
        return content;
    }

#if NET11_0_OR_GREATER
    /// <summary>
    /// Gets the <see cref="ZstdCompressor"/> to use for requests compressed with Zstandard.
    /// </summary>
    /// <returns>The <see cref="ZstdCompressor"/> to use.</returns>
    protected ZstdCompressor GetZstdCompressor()
        => this.zstdCompressor ?? throw new InvalidOperationException("Zstandard compression is not enabled.");
#endif

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, DateTime deadlineUtc, CancellationToken cancellationToken)
    {
        if (GetTimeUntilDeadline(deadlineUtc) is not { } remaining)
        {
            // There is no deadline when neither the exporter nor its caller limits the
            // export (for example an HttpClient with an infinite Timeout), so only the
            // caller's token applies.
            return this.SendHttpRequest(request, cancellationToken);
        }

        if (remaining <= TimeSpan.Zero)
        {
            // Do not start a request once the deadline has passed, for example
            // because serializing the batch used all of the time that was allowed.
            const string Message = "The export deadline was reached before the request was sent.";
            throw new TaskCanceledException(Message, new TimeoutException(Message));
        }

        // HttpClient.Timeout applies to every request and cannot be
        // changed once the client has been used, so a deadline which is
        // reached sooner (e.g. the time left of a ForceFlush timeout) is
        // enforced through the cancellation token instead.
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(remaining);

        try
        {
            return this.SendHttpRequest(request, deadlineCts.Token);
        }
        catch (OperationCanceledException ex) when (deadlineCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Surface the deadline the same way HttpClient reports its own
            // timeout so that the callers' timeout handling applies.
            throw new TaskCanceledException(ex.Message, new TimeoutException(ex.Message, ex));
        }
    }

    private HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken) =>
#if NET
        // Note: SendAsync must be used with HTTP/2 because synchronous send is
        // not supported.
        this.RequireHttp2 || !SynchronousSendSupportedByCurrentPlatform
            ? this.HttpClient.SendAsync(request, this.CompletionOption, cancellationToken).GetAwaiter().GetResult()
            : this.HttpClient.Send(request, this.CompletionOption, cancellationToken);
#else
        this.HttpClient.SendAsync(request, this.CompletionOption, cancellationToken).GetAwaiter().GetResult();
#endif
}
