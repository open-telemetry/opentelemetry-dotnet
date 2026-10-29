// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using System.Net;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

/// <summary>
/// Serializes a batch of spans carrying built-in <see cref="IUtf8SpanFormattable"/> attributes and
/// returns the export buffer, as each export does, with the UTF-8 span path disabled and enabled.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Utf8SpansConfig))]
public class OtlpUtf8SpanFormattableExportBenchmarks
{
    private readonly SerializationBuffer serializationBuffer = new(ProtobufSerializer.InitialBufferSize);
    private readonly SdkLimitOptions sdkLimitOptions = new();
    private Activity[] activities = [];

    [Params(512)]
    public int SpanCount { get; set; }

    /// <summary>
    /// Gets or sets the attribute value length limit in chars, where <c>0</c> means no limit.
    /// </summary>
    [Params(0, 16 * 1024, 64 * 1024)]
    public int MaxLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.sdkLimitOptions.AttributeValueLengthLimit = this.MaxLength > 0 ? this.MaxLength : null;

        this.activities = new Activity[this.SpanCount];
        for (var i = 0; i < this.SpanCount; i++)
        {
            var activity = ActivityHelper.CreateTestActivity();
            activity.SetTag("client.address", IPAddress.Parse("192.168.100.200"));
            activity.SetTag("service.version", new Version(10, 0, 1326, 50504));
            this.activities[i] = activity;
        }
    }

    [Benchmark]
    public int Export()
    {
        var buffer = this.serializationBuffer.Rent();
        var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(
            ref buffer,
            0,
            this.sdkLimitOptions,
            Resource.Empty,
            new Batch<Activity>(this.activities, this.activities.Length),
            out var dirtyLength);
        this.serializationBuffer.Return(buffer, writePosition, dirtyLength);
        return writePosition;
    }
}
#endif
