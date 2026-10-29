// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
extern alias OpenTelemetryProtocol;

using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

/// <summary>
/// Compares writing built-in types that implement <see cref="IUtf8SpanFormattable"/>, and are
/// not otherwise special-cased by the tag writer, with the UTF-8 span path disabled and enabled.
/// </summary>
[MemoryDiagnoser]
public class OtlpUtf8SpanFormattableBuiltInTypeBenchmarks
{
    private static readonly ProtobufOtlpTagWriter DisabledWriter = new(new ProtobufOtlpTagWriter.OtlpArrayTagWriter(), enableUtf8Spans: false);
    private static readonly ProtobufOtlpTagWriter EnabledWriter = new(new ProtobufOtlpTagWriter.OtlpArrayTagWriter(), enableUtf8Spans: true);

    private readonly byte[] buffer = new byte[ProtobufSerializer.InitialBufferSize];
    private object value = null!;
    private int? tagValueMaxLength;

    [Params("IntPtr", "UIntPtr", "Int128", "UInt128", "Half", "NFloat", "DateOnly", "TimeOnly", "Version", "IPAddressV4", "IPAddressV6", "IPNetwork", "BigInteger", "Complex", "Rune")]
    public string Type { get; set; } = null!;

    /// <summary>
    /// Gets or sets the attribute value length limit in chars, where <c>0</c> means no limit.
    /// </summary>
    [Params(0, 16 * 1024, 64 * 1024)]
    public int MaxLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.value = this.Type switch
        {
            "IntPtr" => (nint)123456789,
            "UIntPtr" => (nuint)123456789,
            "Int128" => Int128.MaxValue,
            "UInt128" => UInt128.MaxValue,
            "Half" => (Half)3.14159,
            "NFloat" => (NFloat)3.14159,
            "DateOnly" => new DateOnly(2026, 10, 29),
            "TimeOnly" => new TimeOnly(13, 45, 30, 123),
            "Version" => new Version(10, 0, 1326, 50504),
            "IPAddressV4" => IPAddress.Parse("192.168.100.200"),
            "IPAddressV6" => IPAddress.Parse("2001:db8:85a3::8a2e:370:7334"),
            "IPNetwork" => IPNetwork.Parse("10.20.0.0/16"),
            "BigInteger" => BigInteger.Pow(3, 100),
            "Complex" => new Complex(1.5, -2.25),
            "Rune" => new Rune(0x1F600),
            _ => throw new NotSupportedException(this.Type),
        };

        this.tagValueMaxLength = this.MaxLength > 0 ? this.MaxLength : null;
    }

    [Benchmark(Baseline = true)]
    public int Disabled() => this.Write(DisabledWriter);

    [Benchmark]
    public int Enabled() => this.Write(EnabledWriter);

    private int Write(ProtobufOtlpTagWriter writer)
    {
        var state = new ProtobufOtlpTagWriter.OtlpTagWriterState { Buffer = this.buffer };
        writer.WriteKeyValueInternal(ref state, 1, "value", this.value, this.tagValueMaxLength);
        return state.WritePosition;
    }
}
#endif
