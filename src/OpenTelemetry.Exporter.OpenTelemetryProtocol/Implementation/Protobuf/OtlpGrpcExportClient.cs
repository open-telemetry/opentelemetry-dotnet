// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Buffers.Binary;
using System.Net.Http.Headers;
using Grpc.Core;
using OpenTelemetry.Internal;
using OpenTelemetry.Proto.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

/// <summary>Base class for sending OTLP export request over Grpc.</summary>
internal class OtlpGrpcExportClient : IExportClient
{

    internal const string ErrorStartingCallMessage = "Error starting gRPC call.";
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
                return new ExportClientGrpcResponse(false, deadlineUtc, rpcException);
            }

            // We do not need to return back response and deadline for successful response so using cached value.
            return SuccessExportResponse;
        }
        catch (HttpRequestException ex)
        {
            var status = new Status(StatusCode.Unavailable, ErrorStartingCallMessage + " " + ex.Message, ex);

            var rpcException = new RpcException(status);
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, rpcException);

            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: rpcException);
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
        request.Version = new Version(2, 0);

#if NET6_0_OR_GREATER
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif

        foreach (var header in this.Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        Span<byte> data = new Span<byte>(exportRequest, 1, 4);
        var dataLength = contentLength - 5;
        BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);

        request.Content = new ByteArrayContent(exportRequest, 0, contentLength);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");

        return request;
    }

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }
}

