// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

[MemoryDiagnoser]
public class ProtobufOtlpTraceSerializationBenchmarks
{
    private const int InitialBufferSize = 750000;

    private readonly SdkLimitOptions sdkLimitOptions = new();
    private readonly Resource resource = Resource.Empty;
    private Activity[] activities = null!;
    private int batchCount;
    private byte[] steadyStateBuffer = null!;
    private int bufferSize = InitialBufferSize;

    // SpanCount=1 represents the common steady-state case (fits the initial buffer).
    // SpanCount=5000 produces a payload (>1 MB) that forces the buffer-growth path.
    [Params(1, 5000)]
    public int SpanCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.activities = new Activity[this.SpanCount];
        for (var i = 0; i < this.SpanCount; i++)
        {
            this.activities[i] = ActivityHelper.CreateTestActivity();
        }

        this.batchCount = this.SpanCount;

        // Pre-grow a buffer to the final size so the steady-state benchmark
        // never hits the growth path (mirrors a warmed-up exporter instance).
        this.steadyStateBuffer = new byte[InitialBufferSize];
        var batch = new Batch<Activity>(this.activities, this.batchCount);
        ProtobufOtlpTraceSerializer.WriteTraceData(ref this.steadyStateBuffer, 0, this.sdkLimitOptions, this.resource, batch);
    }

    // Warmed-up exporter: the instance buffer is already large enough, so no growth occurs.
    [Benchmark(Baseline = true)]
    public int SteadyState()
    {
        var buffer = this.steadyStateBuffer;
        var batch = new Batch<Activity>(this.activities, this.batchCount);
        return ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, this.sdkLimitOptions, this.resource, batch);
    }

    // Cold start: begins from a freshly allocated initial-size buffer (matching a
    // brand-new exporter instance). For payloads larger than the initial size this
    // exercises the exception-driven resize + full re-serialization path.
    [Benchmark]
    public int ColdStartWithGrowth()
    {
        var buffer = new byte[InitialBufferSize];
        var batch = new Batch<Activity>(this.activities, this.batchCount);
        return ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, this.sdkLimitOptions, this.resource, batch);
    }

    // Models the exporter's rent-per-export path: rent a right-sized buffer from
    // the pool, serialize (growing via the pool if needed), remember the grown
    // size as the next hint, and return the buffer for reuse.
    [Benchmark]
    public int PooledExport()
    {
        var buffer = ProtobufSerializer.RentBuffer(this.bufferSize);
        try
        {
            var batch = new Batch<Activity>(this.activities, this.batchCount);
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, this.sdkLimitOptions, this.resource, batch);
            this.bufferSize = buffer.Length;
            return writePosition;
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }
}
#endif
