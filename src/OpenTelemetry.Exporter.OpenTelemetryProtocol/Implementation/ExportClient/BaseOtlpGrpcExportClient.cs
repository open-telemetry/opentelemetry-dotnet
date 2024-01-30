// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OpenTelemetry.Internal;
#if NETSTANDARD2_1 || NET6_0_OR_GREATER
using Grpc.Net.Client;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over gRPC.</summary>
/// <typeparam name="TRequest">Type of export request.</typeparam>
/// <typeparam name="TResponse">Type of export response.</typeparam>
internal abstract class BaseOtlpGrpcExportClient<TRequest, TResponse> : IExportClient<TRequest, TResponse>
{
    protected BaseOtlpGrpcExportClient(OtlpExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);

        ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options);

        this.Endpoint = new UriBuilder(options.Endpoint).Uri;
        this.Headers = options.GetMetadataFromHeaders();
        this.TimeoutMilliseconds = options.TimeoutMilliseconds;
    }

#if NETSTANDARD2_1 || NET6_0_OR_GREATER
    internal GrpcChannel Channel { get; set; }
#else
    internal Channel Channel { get; set; }
#endif

    internal Uri Endpoint { get; }

    internal Metadata Headers { get; }

    internal int TimeoutMilliseconds { get; }

    /// <inheritdoc/>
    public abstract bool SendExportRequest(TRequest request, out TResponse response, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public virtual bool Shutdown(int timeoutMilliseconds)
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
            return Task.WaitAny(new Task[] { this.Channel.ShutdownAsync(), Task.Delay(timeoutMilliseconds) }) == 0;
        }
    }
}
