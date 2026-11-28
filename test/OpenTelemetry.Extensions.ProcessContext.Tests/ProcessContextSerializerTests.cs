// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Resources;
using ProcessContextMessage = OpenTelemetry.Proto.ProcessContext.V1Development.ProcessContext;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

public sealed class ProcessContextSerializerTests
{
    private static readonly string[] Strings = ["a", "b"];
    private static readonly bool[] Bools = [true, false];
    private static readonly string[] Xy = ["x", "y"];

    [Fact]
    public void EmptyResourceStillWritesResourceField()
    {
        var bytes = ProcessContextSerializer.Serialize(Resource.Empty, attributes: null);

        // Tag for field 1 (LEN) followed by a zero length: never an empty payload.
        Assert.Equal(new byte[] { 0x0A, 0x00 }, bytes);

        var decoded = ProcessContextMessage.Parser.ParseFrom(bytes);
        Assert.NotNull(decoded.Resource);
        Assert.Empty(decoded.Resource.Attributes);
        Assert.Empty(decoded.Attributes);
    }

    [Fact]
    public void ResourceAttributesOfEveryTypeRoundTrip()
    {
        var resource = new Resource(new Dictionary<string, object>
        {
            ["service.name"] = "checkout",
            ["service.instance.id"] = "7f6c1e2a-1b2c-4d5e-8f90-123456789abc",
            ["bool"] = true,
            ["long"] = -42L,
            ["double"] = 3.5,
            ["strings"] = Strings,
            ["bools"] = Bools,
            ["longs"] = new[] { 1L, long.MaxValue, long.MinValue },
            ["doubles"] = new[] { 1.25, -0.5 },
        });

        var decoded = ProcessContextMessage.Parser.ParseFrom(ProcessContextSerializer.Serialize(resource, attributes: null));
        var attributes = decoded.Resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(9, attributes.Count);
        Assert.Equal("checkout", attributes["service.name"].StringValue);
        Assert.Equal("7f6c1e2a-1b2c-4d5e-8f90-123456789abc", attributes["service.instance.id"].StringValue);
        Assert.True(attributes["bool"].BoolValue);
        Assert.Equal(-42L, attributes["long"].IntValue);
        Assert.Equal(3.5, attributes["double"].DoubleValue);
        Assert.Equal(["a", "b"], attributes["strings"].ArrayValue.Values.Select(v => v.StringValue));
        Assert.Equal([true, false], attributes["bools"].ArrayValue.Values.Select(v => v.BoolValue));
        Assert.Equal([1L, long.MaxValue, long.MinValue], attributes["longs"].ArrayValue.Values.Select(v => v.IntValue));
        Assert.Equal([1.25, -0.5], attributes["doubles"].ArrayValue.Values.Select(v => v.DoubleValue));
        Assert.Empty(decoded.Attributes);
    }

    [Fact]
    public void ExtraAttributesAreWrittenToTheirOwnField()
    {
        var bytes = ProcessContextSerializer.Serialize(
            new Resource(new Dictionary<string, object> { ["service.name"] = "svc" }),
            [new("threadlocal.schema_version", "tlsdesc_v1_dev"), new("count", 3)]);

        var decoded = ProcessContextMessage.Parser.ParseFrom(bytes);
        Assert.Equal("svc", Assert.Single(decoded.Resource.Attributes).Value.StringValue);
        Assert.Equal(2, decoded.Attributes.Count);
        Assert.Equal("threadlocal.schema_version", decoded.Attributes[0].Key);
        Assert.Equal("tlsdesc_v1_dev", decoded.Attributes[0].Value.StringValue);
        Assert.Equal(3L, decoded.Attributes[1].Value.IntValue);
    }

    [Fact]
    public void OutputMatchesGoogleProtobufEncoding()
    {
        // The hand-written writer must produce the canonical encoding, byte for byte.
        var resource = new Resource(new Dictionary<string, object>
        {
            ["service.name"] = "svc",
            ["long.value"] = new string('x', 300), // Forces multi-byte lengths at every nesting level.
            ["n"] = 150L,
            ["d"] = 2.0,
            ["arr"] = Xy,
        });

        var expected = new ProcessContextMessage { Resource = new Proto.Resource.V1.Resource() };
        foreach (var attribute in resource.Attributes)
        {
            expected.Resource.Attributes.Add(new KeyValue { Key = attribute.Key, Value = ToAnyValue(attribute.Value) });
        }

        Assert.Equal(expected.ToByteArray(), ProcessContextSerializer.Serialize(resource, attributes: null));
    }

    [Fact]
    public void LargePayloadRoundTrips()
    {
        var attributes = Enumerable.Range(0, 500).ToDictionary(i => $"key.{i:D4}", i => (object)new string('v', i % 50));
        var bytes = ProcessContextSerializer.Serialize(new Resource(attributes), attributes: null);

        Assert.True(bytes.Length > 16_384, "The payload should need a three byte length prefix.");
        var decoded = ProcessContextMessage.Parser.ParseFrom(bytes);
        Assert.Equal(500, decoded.Resource.Attributes.Count);
        Assert.Equal(new string('v', 49), decoded.Resource.Attributes.Single(kv => kv.Key == "key.0049").Value.StringValue);
    }

    [Fact]
    public void DuplicateKeysKeepTheLastValue()
    {
        var bytes = ProcessContextSerializer.Serialize(
            Resource.Empty,
            [new("k", "first"), new("other", "x"), new("k", "second")]);

        var decoded = ProcessContextMessage.Parser.ParseFrom(bytes);
        Assert.Equal(["k", "other"], decoded.Attributes.Select(kv => kv.Key));
        Assert.Equal("second", decoded.Attributes[0].Value.StringValue);
    }

    [Fact]
    public void UnsupportedValuesAndEmptyKeysAreDropped()
    {
        var bytes = ProcessContextSerializer.Serialize(
            Resource.Empty,
            [new("ok", "yes"), new("object", new object()), new(string.Empty, "no key"), new("null", null), new("mixed", new object[] { "a", new object() })]);

        var decoded = ProcessContextMessage.Parser.ParseFrom(bytes);
        Assert.Equal("ok", Assert.Single(decoded.Attributes).Key);
    }

    [Fact]
    public void InvalidUtf16IsWrittenAsValidUtf8()
    {
        var bytes = ProcessContextSerializer.Serialize(
            new Resource(new Dictionary<string, object> { ["service.name"] = "bad\uD800name" }),
            attributes: null);

        // Google.Protobuf throws on invalid UTF-8 in a string field.
        var decoded = ProcessContextMessage.Parser.ParseFrom(bytes);
        Assert.Equal("bad�name", decoded.Resource.Attributes[0].Value.StringValue);
    }

    private static AnyValue ToAnyValue(object value) => value switch
    {
        string s => new AnyValue { StringValue = s },
        long l => new AnyValue { IntValue = l },
        double d => new AnyValue { DoubleValue = d },
        bool b => new AnyValue { BoolValue = b },
        string[] array => new AnyValue { ArrayValue = new ArrayValue { Values = { array.Select(s => new AnyValue { StringValue = s }) } } },
        _ => throw new NotSupportedException(),
    };
}
