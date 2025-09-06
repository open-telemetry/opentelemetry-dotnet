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
        this.CompressionEnabled = options.CompressPayload;
    }

    internal HttpClient HttpClient { get; }

    internal bool CompressionEnabled { get; }

    internal Uri Endpoint { get; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    internal abstract MediaTypeHeaderValue MediaTypeHeader { get; }

    internal virtual bool RequireHttp2 => false;

    protected abstract string? ContentEncodingHeader { get; }

    public abstract ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();
        return true;
    }

    protected HttpRequestMessage CreateHttpRequest(byte[] buffer, int contentLength)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint);

        if (this.RequireHttp2)
            {
                request.Version = Http2RequestVersion;

#if NET6_0_OR_GREATER
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif
            }

        foreach (var header in this.Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        var data = buffer;

        if (this.CompressionEnabled)
        {
            data = this.Compress(buffer, contentLength);
            if (this.ContentEncodingHeader != null)
            {
                request.Headers.Add("Content-Encoding", this.ContentEncodingHeader);
            }
        }

        request.Content = new ByteArrayContent(data, 0, data.Length);
        request.Content.Headers.ContentType = this.MediaTypeHeader;

        return request;
    }

    protected abstract byte[] Compress(byte[] data, int contentLength);

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
#if NET
        // Note: SendAsync must be used with HTTP/2 because synchronous send is
        // not supported.
        return this.RequireHttp2 || !SynchronousSendSupportedByCurrentPlatform
            ? this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()
            : this.HttpClient.Send(request, cancellationToken);
#else
        return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
#endif
    }
}
