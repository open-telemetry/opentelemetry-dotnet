// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET462_OR_GREATER || NETSTANDARD2_0
using Grpc.Core;
using OpenTelemetry.Internal;

using InternalStatus = OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc.Status;
using InternalStatusCode = OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc.StatusCode;
using Status = Grpc.Core.Status;
using StatusCode = Grpc.Core.StatusCode;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class GrpcExportClient : IExportClient
{
    private static readonly ExportClientGrpcResponse SuccessExportResponse = new(
        success: true,
        deadlineUtc: default,
        exception: null,
        status: null,
        grpcStatusDetailsHeader: null);

    private static readonly Marshaller<byte[]> ByteArrayMarshaller = Marshallers.Create(
        serializer: static input => input,
        deserializer: static data => data);

    private readonly Method<byte[], byte[]> exportMethod;

    private readonly CallInvoker callInvoker;

    public GrpcExportClient(OtlpExporterOptions options, string signalPath)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);
        Guard.ThrowIfNull(signalPath);

        var exporterEndpoint = options.Endpoint.AppendPathIfNotPresent(signalPath);
        this.Endpoint = new UriBuilder(exporterEndpoint).Uri;
        this.Channel = options.CreateChannel();
        this.Headers = options.GetMetadataFromHeaders();

        var serviceAndMethod = signalPath.Split('/');
        this.exportMethod = new Method<byte[], byte[]>(MethodType.Unary, serviceAndMethod[0], serviceAndMethod[1], ByteArrayMarshaller, ByteArrayMarshaller);
        this.callInvoker = this.Channel.CreateCallInvoker();
    }

    internal Channel Channel { get; }

    internal Uri Endpoint { get; }

    internal Metadata Headers { get; }

    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            var contentSpan = buffer.AsSpan(0, contentLength);
            this.callInvoker?.BlockingUnaryCall(this.exportMethod, null, new CallOptions(this.Headers, deadlineUtc, cancellationToken), contentSpan.ToArray());
            return SuccessExportResponse;
        }
        catch (RpcException rpcException)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, rpcException);
            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: rpcException, ConvertGrpcStatusToStatus(rpcException.Status), rpcException.Trailers.ToString());
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        if (this.Channel == null)
        {
            return true;
        }

        if (timeoutMilliseconds == -1)
        {
            this.Channel.ShutdownAsync().Wait();
            return true;
        }
        else
        {
            return Task.WaitAny([this.Channel.ShutdownAsync(), Task.Delay(timeoutMilliseconds)]) == 0;
        }
    }

    private static InternalStatus ConvertGrpcStatusToStatus(Status grpcStatus) => grpcStatus.StatusCode switch
    {
        StatusCode.OK => new InternalStatus(InternalStatusCode.OK, grpcStatus.Detail),
        StatusCode.Cancelled => new InternalStatus(InternalStatusCode.Cancelled, grpcStatus.Detail),
        StatusCode.Unknown => new InternalStatus(InternalStatusCode.Unknown, grpcStatus.Detail),
        StatusCode.InvalidArgument => new InternalStatus(InternalStatusCode.InvalidArgument, grpcStatus.Detail),
        StatusCode.DeadlineExceeded => new InternalStatus(InternalStatusCode.DeadlineExceeded, grpcStatus.Detail),
        StatusCode.NotFound => new InternalStatus(InternalStatusCode.NotFound, grpcStatus.Detail),
        StatusCode.AlreadyExists => new InternalStatus(InternalStatusCode.AlreadyExists, grpcStatus.Detail),
        StatusCode.PermissionDenied => new InternalStatus(InternalStatusCode.PermissionDenied, grpcStatus.Detail),
        StatusCode.Unauthenticated => new InternalStatus(InternalStatusCode.Unauthenticated, grpcStatus.Detail),
        StatusCode.ResourceExhausted => new InternalStatus(InternalStatusCode.ResourceExhausted, grpcStatus.Detail),
        StatusCode.FailedPrecondition => new InternalStatus(InternalStatusCode.FailedPrecondition, grpcStatus.Detail),
        StatusCode.Aborted => new InternalStatus(InternalStatusCode.Aborted, grpcStatus.Detail),
        StatusCode.OutOfRange => new InternalStatus(InternalStatusCode.OutOfRange, grpcStatus.Detail),
        StatusCode.Unimplemented => new InternalStatus(InternalStatusCode.Unimplemented, grpcStatus.Detail),
        StatusCode.Internal => new InternalStatus(InternalStatusCode.Internal, grpcStatus.Detail),
        StatusCode.Unavailable => new InternalStatus(InternalStatusCode.Unavailable, grpcStatus.Detail),
        StatusCode.DataLoss => new InternalStatus(InternalStatusCode.DataLoss, grpcStatus.Detail),
        _ => new InternalStatus(InternalStatusCode.Unknown, grpcStatus.Detail),
    };
}
#endif
