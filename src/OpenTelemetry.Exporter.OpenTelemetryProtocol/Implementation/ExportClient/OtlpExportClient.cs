// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net;
using System.Net.Http.Headers;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal abstract class OtlpExportClient : IExportClient
{
    private static readonly Version Http2RequestVersion = new(2, 0);

#if NET
    private static readonly bool SynchronousSendSupportedByCurrentPlatform;

    static OtlpExportClient()
    {
#if NET
        // See: https://github.com/dotnet/runtime/blob/280f2a0c60ce0378b8db49adc0eecc463d00fe5d/src/libraries/System.Net.Http/src/System/Net/Http/HttpClientHandler.AnyMobile.cs#L767
        SynchronousSendSupportedByCurrentPlatform = !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS()
            && !OperatingSystem.IsTvOS()
            && !OperatingSystem.IsBrowser();
#endif
    }
#endif

    protected OtlpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(httpClient);
        Guard.ThrowIfNull(signalPath);

        Uri exporterEndpoint;
        if (options.Protocol == OtlpExportProtocol.Grpc)
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

        // TODO: Support compression.

        request.Content = new ByteArrayContent(buffer, 0, contentLength);
        request.Content.Headers.ContentType = this.MediaTypeHeader;

        return request;
    }

    protected (Uri Uri, HttpMethod Method, Dictionary<string, string> Headers, byte[] Content, string ContentType)
    CreateSynchronousRequestParams(byte[] buffer, int contentLength)
    {
        Uri uri = this.Endpoint;


        HttpMethod method = HttpMethod.Post;

        var headers = new Dictionary<string, string>();
        foreach (var header in this.Headers)
        {
            headers[header.Key] = header.Value;
        }


        byte[] content;
        if (contentLength < buffer.Length)
        {
            content = new byte[contentLength];
            Array.Copy(buffer, 0, content, 0, contentLength);
        }
        else
        {
            content = buffer;
        }

        string contentType = this.MediaTypeHeader.ToString();

        return (uri, method, headers, content, contentType);
    }

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

    protected HttpResponseMessage SendHttpRequestSynchronous(Uri uri, HttpMethod method, Dictionary<string, string> headers = null, byte[] content = null, string contentType = null, CancellationToken cancellationToken = default)
    {
        var webRequest = (HttpWebRequest)WebRequest.Create(uri);
        webRequest.Method = method.ToString();
        webRequest.AllowAutoRedirect = true;

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (WebHeaderCollection.IsRestricted(header.Key))
                {
                    switch (header.Key.ToLowerInvariant())
                    {
                        case "accept":
                            webRequest.Accept = header.Value;
                            break;
                        case "connection":
                            webRequest.Connection = header.Value;
                            break;
                        case "content-type":
                            break;
                        case "user-agent":
                            webRequest.UserAgent = header.Value;
                            break;
                        case "content-length":
                            break;
                        case "expect":
                            webRequest.Expect = header.Value;
                            break;
                        case "date":
                            webRequest.Date = DateTime.Parse(header.Value);
                            break;
                        case "host":
                            break;
                        case "if-modified-since":
                            webRequest.IfModifiedSince = DateTime.Parse(header.Value);
                            break;
                        case "range":
                            string[] range = header.Value.Split('=', ',');
                            if (range.Length >= 2 && range[0].Trim().Equals("bytes"))
                            {
                                string[] startEnd = range[1].Split('-');
                                if (startEnd.Length >= 2)
                                {
                                    long start = long.Parse(startEnd[0]);
                                    long end = long.Parse(startEnd[1]);
                                    webRequest.AddRange(start, end);
                                }
                            }
                            break;
                        case "referer":
                            webRequest.Referer = header.Value;
                            break;
                        case "transfer-encoding":
                            webRequest.TransferEncoding = header.Value;
                            break;
                    }
                }
                else
                {
                    webRequest.Headers.Add(header.Key, header.Value);
                }
            }
        }


        if (!string.IsNullOrEmpty(contentType))
        {
            webRequest.ContentType = contentType;
        }


        if (content != null && content.Length > 0)
        {
            webRequest.ContentLength = content.Length;

            using (var requestStream = webRequest.GetRequestStream())
            {
                requestStream.Write(content, 0, content.Length);
            }
        }
        else
        {
            webRequest.ContentLength = 0;
        }

        cancellationToken.Register(webRequest.Abort);

        try
        {
            using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                return CreateResponseFromWebResponse(webResponse);
            }
        }
        catch (WebException ex)
        {
            if (ex.Response is HttpWebResponse errorResponse)
            {
                return CreateResponseFromWebResponse(errorResponse);
            }

            // For cases where there's no response (timeout, etc.)
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = ex.Message,
            };

            return response;
        }
        catch (Exception ex)
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = ex.Message,
            };

            return response;
        }
    }

    private static HttpResponseMessage CreateResponseFromWebResponse(HttpWebResponse webResponse)
    {
        var response = new HttpResponseMessage(webResponse.StatusCode)
        {
            ReasonPhrase = webResponse.StatusDescription,
            Version = new Version(webResponse.ProtocolVersion.ToString()),
        };

        foreach (string headerName in webResponse.Headers.AllKeys)
        {
            if (headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Location", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Range", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            response.Headers.TryAddWithoutValidation(headerName, webResponse.Headers[headerName]);
        }


        if (webResponse.ContentLength != 0)
        {
            var responseStream = webResponse.GetResponseStream();
            var memoryStream = new MemoryStream();

            if (responseStream != null)
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                memoryStream.Position = 0;
            }

            response.Content = new ByteArrayContent(memoryStream.ToArray());

            if (!string.IsNullOrEmpty(webResponse.ContentType))
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(webResponse.ContentType);
            }

            if (webResponse.ContentLength > 0)
            {
                response.Content.Headers.ContentLength = webResponse.ContentLength;
            }

            var contentEncoding = webResponse.Headers["Content-Encoding"];
            if (!string.IsNullOrEmpty(contentEncoding))
            {
                response.Content.Headers.TryAddWithoutValidation("Content-Encoding", contentEncoding);
            }

            var contentLanguage = webResponse.Headers["Content-Language"];
            if (!string.IsNullOrEmpty(contentLanguage))
            {
                response.Content.Headers.TryAddWithoutValidation("Content-Language", contentLanguage);
            }

            // Add other content headers if needed
            var contentDisposition = webResponse.Headers["Content-Disposition"];
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                response.Content.Headers.TryAddWithoutValidation("Content-Disposition", contentDisposition);
            }
        }
        else
        {
            response.Content = new ByteArrayContent([]);
        }

        return response;
    }
}
