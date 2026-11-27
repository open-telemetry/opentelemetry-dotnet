// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

/*
Benchmarks for OtlpRetry focusing on:
1. Lock-free random number generation (ThreadSafeRandom vs locked Random)
2. Overflow-free throttle delay clamping (Math.Min/clamp vs try/catch OverflowException)

Run with:
  dotnet run --project test/Benchmarks/Benchmarks.csproj -c Release -- --filter "*OtlpRetryBenchmarks*"
*/

namespace Benchmarks.Exporter;

[MemoryDiagnoser]
public class OtlpRetryBenchmarks
{
    private readonly ExportClientGrpcResponse retryableResponse;
    private readonly ExportClientGrpcResponse retryableResponseWithLargeThrottle;

    public OtlpRetryBenchmarks()
    {
        this.retryableResponse = new ExportClientGrpcResponse(
            success: false,
            deadlineUtc: DateTime.UtcNow.AddHours(1),
            exception: null,
            status: new Status(StatusCode.Unavailable, "Unavailable"),
            grpcStatusDetailsHeader: null);

        // Build a gRPC response whose throttle delay exceeds int.MaxValue milliseconds.
        // Before the fix this would throw and catch OverflowException on every call.
        var throttleDuration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(TimeSpan.FromDays(30));
        var grpcStatus = new Google.Rpc.Status
        {
            Code = (int)StatusCode.ResourceExhausted,
            Message = "Throttled",
            Details =
            {
                Google.Protobuf.WellKnownTypes.Any.Pack(new Google.Rpc.RetryInfo
                {
                    RetryDelay = throttleDuration,
                }),
            },
        };

        this.retryableResponseWithLargeThrottle = new ExportClientGrpcResponse(
            success: false,
            deadlineUtc: DateTime.UtcNow.AddDays(60),
            exception: null,
            status: new Status(StatusCode.ResourceExhausted, "Throttled"),
            grpcStatusDetailsHeader: Convert.ToBase64String(grpcStatus.ToByteArray()));
    }

    /// <summary>
    /// Exercises the jitter random path (no throttle delay) which previously
    /// took <c>lock (Random)</c> on .NET Framework / .NET Standard.
    /// </summary>
    [Benchmark]
    public int RetryWithJitter()
    {
        OtlpRetry.TryGetGrpcRetryResult(this.retryableResponse, OtlpRetry.InitialBackoffMilliseconds, out var result);
        return result.NextRetryDelayMilliseconds;
    }

    /// <summary>
    /// Exercises the throttle-delay-to-int conversion path which previously
    /// threw and caught <see cref="OverflowException"/> for large delays.
    /// </summary>
    [Benchmark]
    public int RetryWithLargeThrottleDelay()
    {
        OtlpRetry.TryGetGrpcRetryResult(this.retryableResponseWithLargeThrottle, OtlpRetry.InitialBackoffMilliseconds, out var result);
        return result.NextRetryDelayMilliseconds;
    }
}
