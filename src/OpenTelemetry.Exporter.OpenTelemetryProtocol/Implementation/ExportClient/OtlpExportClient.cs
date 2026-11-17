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

#if NET
    private readonly Func<HttpClient>? createReplacementHttpClient;
#endif

    private HttpClient httpClient;

#if NET
    private int replacementInProgress;
#endif

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
        this.httpClient = httpClient;

#if NET
        // Only an HttpClient that the exporter created for itself is the exporter's to replace.
        if (ReferenceEquals(options.HttpClientFactory, options.DefaultHttpClientFactory))
        {
            this.createReplacementHttpClient = options.DefaultHttpClientFactory;
        }
#endif

        this.CompressionEnabled = options.Compression == OtlpExportCompression.GZip;
        this.MaxResponseSizeBytes = options.MaxResponseSizeBytes;
    }

    internal HttpClient HttpClient => Volatile.Read(ref this.httpClient);

    internal Uri Endpoint { get; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    internal bool CompressionEnabled { get; }

    /// <summary>
    /// Gets the maximum size, in bytes, of a response the exporter will accept.
    /// </summary>
    internal int MaxResponseSizeBytes { get; }

    internal abstract MediaTypeHeaderValue MediaTypeHeader { get; }

    internal virtual long HeaderBytesSize => 0;

    internal virtual bool RequireHttp2 => false;

    internal virtual HttpCompletionOption CompletionOption => HttpCompletionOption.ResponseHeadersRead;

    public abstract ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        try
        {
            this.HttpClient.CancelPendingRequests();
        }
        catch (ObjectDisposedException)
        {
            // The HttpClient was replaced and disposed of concurrently,
            // which has already canceled the requests that were pending on it.
        }

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

    protected HttpRequestMessage CreateHttpRequest(byte[] buffer, int contentLength)
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

        request.Content = this.CreateHttpContent(buffer, contentLength);

        return request;
    }

    /// <summary>
    /// Creates the <see cref="HttpContent"/> for a request. Override in subclasses to
    /// customise content creation (e.g. to apply compression).
    /// </summary>
    /// <param name="buffer">The serialized protobuf payload buffer.</param>
    /// <param name="contentLength">The number of bytes within <paramref name="buffer"/> that make up the message.</param>
    /// <returns>An <see cref="HttpContent"/> representing the export payload.</returns>
    protected virtual HttpContent CreateHttpContent(byte[] buffer, int contentLength)
    {
#if NET
        var content = new ReadOnlyMemoryContent(buffer.AsMemory(0, contentLength));
#else
        var content = new ByteArrayContent(buffer, 0, contentLength);
#endif
        content.Headers.ContentType = this.MediaTypeHeader;
        return content;
    }

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

    private HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpClient = this.HttpClient;

#if NET
        try
        {
            // Note: SendAsync must be used with HTTP/2 because synchronous send is
            // not supported.
            return this.RequireHttp2 || !SynchronousSendSupportedByCurrentPlatform
                ? httpClient.SendAsync(request, this.CompletionOption, cancellationToken).GetAwaiter().GetResult()
                : httpClient.Send(request, this.CompletionOption, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.VersionNegotiationError)
        {
            // Once a connection to the endpoint has been established without HTTP/2
            // being negotiated, the HttpClient does not attempt HTTP/2 with the
            // endpoint again for as long as it keeps being used, so every later
            // request that requires HTTP/2 fails without a connection being opened,
            // even after the endpoint supports HTTP/2 again. This request has failed
            // either way, but the next one is sent with a new HttpClient so that
            // the protocol is negotiated again.
            this.ReplaceHttpClient(httpClient);

            throw;
        }
#else
        return httpClient.SendAsync(request, this.CompletionOption, cancellationToken).GetAwaiter().GetResult();
#endif
    }

#if NET
    private void ReplaceHttpClient(HttpClient current)
    {
        if (this.createReplacementHttpClient is not { } createReplacement ||
            Interlocked.CompareExchange(ref this.replacementInProgress, 1, 0) != 0)
        {
            // The HttpClient is not the exporter's to replace,
            // or it is already being replaced.
            return;
        }

        // Only the caller that holds the gate writes the HttpClient, so whether
        // it has already been replaced must be checked while the gate is held.
        if (!ReferenceEquals(this.HttpClient, current))
        {
            Volatile.Write(ref this.replacementInProgress, 0);
            return;
        }

        // Creating an HttpClient can block, for example while certificates are
        // checked for revocation, which does not respect the deadline of an export.
        // It is therefore not done on the thread that is exporting, so that the
        // failed export is not held up. A dedicated thread is used so that the
        // replacement does not depend on the thread pool, and the next export
        // is sent with the replacement once it is ready.
        try
        {
            _ = Task.Factory.StartNew(
                () => this.CreateReplacementHttpClient(current, createReplacement),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        catch (Exception)
        {
            // The thread could not be started. Keep the current HttpClient so that the
            // original failure is reported, and try again after the next failure.
            Volatile.Write(ref this.replacementInProgress, 0);
        }
    }

    private void CreateReplacementHttpClient(HttpClient current, Func<HttpClient> createReplacement)
    {
        try
        {
            HttpClient replacement;

            try
            {
                replacement = createReplacement();
            }
            catch (Exception)
            {
                // The replacement could not be created, for example because a
                // certificate file could not be loaded. Keep the current HttpClient
                // and try again after the next failure.
                return;
            }

            // Only a single replacement is attempted at a time, so nothing else writes the HttpClient.
            Volatile.Write(ref this.httpClient, replacement);

            current.Dispose();
        }
        finally
        {
            Volatile.Write(ref this.replacementInProgress, 0);
        }
    }
#endif
}
