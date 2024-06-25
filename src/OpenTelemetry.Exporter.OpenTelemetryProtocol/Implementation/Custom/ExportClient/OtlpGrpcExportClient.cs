// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Buffers.Binary;
using System.Net.Http.Headers;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.ExportClient;

/// <summary>Base class for sending OTLP export request over Grpc.</summary>
internal class OtlpGrpcExportClient : IExportClient
{
    internal const string ErrorStartingCallMessage = "Error starting gRPC call.";
    private static readonly MediaTypeHeaderValue MediaHeaderValue = new MediaTypeHeaderValue("application/grpc");
    private static readonly Version Http2RequestVersion = new Version(2, 0);
    private static readonly ExportClientHttpResponse SuccessExportResponse = new ExportClientHttpResponse(success: true, deadlineUtc: default, response: null, exception: null);

    internal OtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(httpClient);
        Guard.ThrowIfNull(signalPath);
        Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);

        Uri exporterEndpoint = options.Endpoint.AppendPathIfNotPresent(signalPath);
        this.Endpoint = new UriBuilder(exporterEndpoint).Uri;
        this.Headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
        this.HttpClient = httpClient;
    }

    internal HttpClient HttpClient { get; }

    internal Uri Endpoint { get; set; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    public ExportClientResponse SendExportRequest(byte[] exportRequest, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = this.CreateHttpRequest(exportRequest, contentLength);

            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            GrpcProtocolHelper.ProcessHttpResponse(httpResponse, out var rpcException);

            if (rpcException != null)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, rpcException);

                return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: rpcException);
            }

            // We do not need to return back response and deadline for successful response so using cached value.
            return SuccessExportResponse;
        }
        catch (Exception ex)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.sendasync?view=net-8.0#remarks
            RpcException rpcException = null;
            if (ex is HttpRequestException)
            {
                var status = new Status(StatusCode.Unavailable, ErrorStartingCallMessage + " " + ex.Message, ex);

                rpcException = new RpcException(status);

                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, rpcException);

                return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: rpcException);
            }
            else if (ex is TaskCanceledException)
            {
                // grpc-dotnet sets the timer for tracking deadline.
                // https://github.com/grpc/grpc-dotnet/blob/1416340c85bb5925b5fed0c101e7e6de71e367e0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L799-L803
                if (ex.InnerException is TimeoutException)
                {
                    var status = new Status(StatusCode.DeadlineExceeded, string.Empty);

                    // TODO: pre-allocate
                    rpcException = new RpcException(status);

                    OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, rpcException);

                    return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: rpcException);
                }
            }

            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: ex);

            // TODO: Handle additional exception types (OperationCancelledException)
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();
        return true;
    }

    public HttpRequestMessage CreateHttpRequest(byte[] exportRequest, int contentLength)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint);
        request.Version = Http2RequestVersion;

#if NET6_0_OR_GREATER
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif

        foreach (var header in this.Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        // Grpc payload consists of 3 parts
        // byte 0 - Specifying if the payload is compressed.
        // 1-4 byte - Specifies the length of payload in big endian format.
        // 5 and above -  Protobuf serialized data.
        Span<byte> data = new Span<byte>(exportRequest, 1, 4);
        var dataLength = contentLength - 5;
        BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);

        request.Content = new ByteArrayContent(exportRequest, 0, contentLength);
        request.Content.Headers.ContentType = MediaHeaderValue;

        return request;
    }

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // grpc-dotnet calls specifies the HttpCompletion.ResponseHeadersRead.
        // However, it is useful specifically for streaming calls?
        // https://github.com/grpc/grpc-dotnet/blob/1416340c85bb5925b5fed0c101e7e6de71e367e0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L485-L486
        return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }
}

