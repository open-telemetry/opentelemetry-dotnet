// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
#if NET
using System.Buffers;
#endif
using System.Net.Http.Headers;
using System.Text;
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
    }

    internal HttpClient HttpClient { get; }

    internal Uri Endpoint { get; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    internal abstract MediaTypeHeaderValue MediaTypeHeader { get; }

    internal virtual bool RequireHttp2 => false;

    public abstract ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();
        return true;
    }

    protected internal static string? TryGetResponseBody(HttpResponseMessage? httpResponse, CancellationToken cancellationToken)
    {
        if (httpResponse?.Content == null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
#if NET
            var stream = httpResponse.Content.ReadAsStream(cancellationToken);
#else
            var stream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif

            // See https://github.com/open-telemetry/opentelemetry-proto/pull/781
            const int MessageSizeLimit = 4 * 1024 * 1024; // 4MiB

            var length = GetBufferLength(stream, MessageSizeLimit);

#if NET
            var buffer = ArrayPool<byte>.Shared.Rent(length);
#else
            var buffer = new byte[length];
#endif

            var count = 0;

            // Read raw bytes so the size limit applies to bytes rather than characters
            while (count < length && !cancellationToken.IsCancellationRequested)
            {
                var read = stream.Read(buffer, count, length - count);

                if (read is 0)
                {
                    break;
                }

                count += read;
            }

            // Decode using the charset from the response content headers, if available
            var encoding = GetEncoding(httpResponse.Content.Headers.ContentType?.CharSet);
            var result = encoding.GetString(buffer, 0, count);

#if NET
            ArrayPool<byte>.Shared.Return(buffer);
#endif

            return result;
        }
        catch (Exception)
        {
            return null;
        }

        static int GetBufferLength(Stream stream, int limit)
        {
            try
            {
                // Avoid allocating an overly large buffer if the stream is smaller than the size limit
                return stream.Length < limit ? (int)stream.Length : limit;
            }
            catch (Exception)
            {
                // Not all Stream types support Length, so default to the maximum
                return limit;
            }
        }

        static Encoding GetEncoding(string? name)
        {
            Encoding encoding = Encoding.UTF8;

            if (!string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    encoding = Encoding.GetEncoding(name);
                }
                catch (Exception)
                {
                    // Invalid encoding name
                }
            }

            return encoding;
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
            request.Headers.Add(header.Key, header.Value);
        }

        // TODO: Support compression.

        request.Content = new ByteArrayContent(buffer, 0, contentLength);
        request.Content.Headers.ContentType = this.MediaTypeHeader;

        return request;
    }

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken) =>
#if NET
        // Note: SendAsync must be used with HTTP/2 because synchronous send is
        // not supported.
        this.RequireHttp2 || !SynchronousSendSupportedByCurrentPlatform
            ? this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()
            : this.HttpClient.Send(request, cancellationToken);
#else
        this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
#endif
}
