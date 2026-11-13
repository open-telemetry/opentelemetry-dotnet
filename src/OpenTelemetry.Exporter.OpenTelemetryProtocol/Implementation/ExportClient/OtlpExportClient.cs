// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics.Tracing;
using System.Net.Http.Headers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
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
    /// The maximum length, in characters, of a partial success error message
    /// to include in the diagnostic log, keeping the event well within the
    /// ETW event size limit (about 64 KiB).
    /// </summary>
    internal const int MaxLoggedErrorMessageLength = 4 * 1024;

    private const string TruncatedMarker = "[TRUNCATED]";

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

    protected OtlpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath, OtlpSignalType? signalType = null)
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
        this.CompressionEnabled = options.Compression == OtlpExportCompression.GZip;
        this.MaxResponseSizeBytes = options.MaxResponseSizeBytes;
        this.SignalType = signalType;
    }

    internal HttpClient HttpClient { get; }

    internal Uri Endpoint { get; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    internal bool CompressionEnabled { get; }

    /// <summary>
    /// Gets the maximum size, in bytes, of a response the exporter will accept.
    /// </summary>
    internal int MaxResponseSizeBytes { get; }

    /// <summary>
    /// Gets the signal the client exports, if known, which names the rejected items of a partial success.
    /// </summary>
    internal OtlpSignalType? SignalType { get; }

    internal abstract MediaTypeHeaderValue MediaTypeHeader { get; }

    internal virtual long HeaderBytesSize => 0;

    internal virtual bool RequireHttp2 => false;

    internal virtual HttpCompletionOption CompletionOption => HttpCompletionOption.ResponseHeadersRead;

    public abstract ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();
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

    /// <summary>
    /// Shortens a server error message to at most <see cref="MaxLoggedErrorMessageLength"/>
    /// characters, followed by a truncation marker.
    /// </summary>
    /// <param name="errorMessage">The error message from the server.</param>
    /// <returns>The message, truncated if it is too long to log.</returns>
    internal static string? TruncateErrorMessage(string? errorMessage)
    {
        if (errorMessage == null || errorMessage.Length <= MaxLoggedErrorMessageLength)
        {
            return errorMessage;
        }

        var length = MaxLoggedErrorMessageLength;

        // Do not split a surrogate pair.
        if (char.IsHighSurrogate(errorMessage[length - 1]))
        {
            length--;
        }

#if NET
        return string.Concat(errorMessage.AsSpan(0, length), TruncatedMarker);
#else
        return errorMessage.Substring(0, length) + TruncatedMarker;
#endif
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
    /// Reads up to <paramref name="maxResponseSizeBytes"/> bytes of the body of a response and
    /// passes them to <paramref name="reader"/>, giving up once <paramref name="deadlineUtc"/> has passed.
    /// </summary>
    /// <typeparam name="TState">The type of the state passed to <paramref name="reader"/>.</typeparam>
    /// <param name="httpResponse">The response to read the body of.</param>
    /// <param name="maxResponseSizeBytes">The maximum number of bytes to read.</param>
    /// <param name="deadlineUtc">The deadline time in UTC for the export request to finish.</param>
    /// <param name="reader">Processes the bytes that were read.</param>
    /// <param name="state">The state to pass to <paramref name="reader"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns><see langword="true"/> if the body was read; otherwise <see langword="false"/>.</returns>
    protected internal static bool TryReadResponseBody<TState>(
        HttpResponseMessage httpResponse,
        int maxResponseSizeBytes,
        DateTime deadlineUtc,
        HttpClientHelpers.ResponseBodyReader<TState> reader,
        ref TState state,
        CancellationToken cancellationToken)
    {
        if (GetTimeUntilDeadline(deadlineUtc) is not { } remaining)
        {
            // There is no deadline, so only the caller's token applies.
            return HttpClientHelpers.TryReadResponseBody(httpResponse, maxResponseSizeBytes, reader, ref state, cancellationToken);
        }

        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        deadlineCts.CancelAfter(remaining);

        return HttpClientHelpers.TryReadResponseBody(httpResponse, maxResponseSizeBytes, reader, ref state, deadlineCts.Token);
    }

    /// <summary>
    /// Gets whether a partial success in a response would be reported, so
    /// that the response does not need to be read if it would not be.
    /// </summary>
    /// <returns><see langword="true"/> if a partial success would be reported.</returns>
    protected static bool IsPartialSuccessReportingEnabled() =>
        OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.All);

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
    /// Reports the <c>partial_success</c> of a successful export response, if it has one.
    /// </summary>
    /// <remarks>
    /// A response with a partial success is still a successful export which must not be
    /// retried, so a response that cannot be read is ignored rather than failing the export.
    /// </remarks>
    /// <param name="buffer">The buffer holding the serialized response message.</param>
    /// <param name="offset">The position of the message in <paramref name="buffer"/>.</param>
    /// <param name="count">The length of the message.</param>
    protected void ReportPartialSuccess(byte[] buffer, int offset, int count)
    {
        if (count == 0
            || !ProtobufOtlpPartialSuccessReader.TryRead(buffer, offset, count, out var rejectedCount, out var errorMessage))
        {
            return;
        }

        errorMessage = TruncateErrorMessage(errorMessage);

        if (rejectedCount > 0)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportPartiallySucceeded(this.Endpoint, this.SignalType, rejectedCount, errorMessage);
        }
        else if (!string.IsNullOrEmpty(errorMessage))
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportSucceededWithWarning(this.Endpoint, errorMessage!);
        }
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
