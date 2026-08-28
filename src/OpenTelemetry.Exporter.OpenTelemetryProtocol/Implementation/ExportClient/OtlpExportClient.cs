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
    private static readonly Version Http2RequestVersion = new(2, 0);

#if NET
    // See: https://github.com/dotnet/runtime/blob/280f2a0c60ce0378b8db49adc0eecc463d00fe5d/src/libraries/System.Net.Http/src/System/Net/Http/HttpClientHandler.AnyMobile.cs#L767
    private static readonly bool SynchronousSendSupportedByCurrentPlatform = !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS()
            && !OperatingSystem.IsTvOS()
            && !OperatingSystem.IsBrowser();
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
        this.HttpClient = httpClient;
        this.CompressionEnabled = options.Compression == OtlpExportCompression.GZip;
        this.MaxResponseSizeBytes = options.MaxResponseSizeBytes;
    }

    internal HttpClient HttpClient { get; }

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
        this.HttpClient.CancelPendingRequests();
        return true;
    }

    protected internal static string? TryGetResponseBody(HttpResponseMessage? httpResponse, int maxResponseSizeBytes, CancellationToken cancellationToken) =>
        HttpClientHelpers.TryGetResponseBodyAsString(httpResponse, maxResponseSizeBytes, cancellationToken);

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
            request.Headers.Add(header.Key, header.Value);
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

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken) =>
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
