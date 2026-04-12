// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class OtlpKvListAttributeTests : IDisposable
{
    private readonly ActivityListener activityListener;

    static OtlpKvListAttributeTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    public OtlpKvListAttributeTests()
    {
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.activityListener);
    }

    [Fact]
    public void EmptyKvList()
    {
        var kvList = new List<KeyValuePair<string, object?>>();
        var kvp = new KeyValuePair<string, object?>("key", kvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal("key", attribute.Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);
        Assert.Empty(attribute.Value.KvlistValue.Values);
    }

    [Fact]
    public void KvListWithSingleStringEntry()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("innerKey", "innerValue"),
        };
        var kvp = new KeyValuePair<string, object?>("key", kvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal("key", attribute.Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);

        var values = attribute.Value.KvlistValue.Values;
        Assert.Single(values);
        Assert.Equal("innerKey", values[0].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, values[0].Value.ValueCase);
        Assert.Equal("innerValue", values[0].Value.StringValue);
    }

    [Fact]
    public void KvListWithMixedValueTypes()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("stringKey", "stringValue"),
            new("intKey", 42L),
            new("boolKey", true),
            new("doubleKey", 3.14),
        };
        var kvp = new KeyValuePair<string, object?>("key", kvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal("key", attribute.Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);

        var values = attribute.Value.KvlistValue.Values;
        Assert.Equal(kvList.Count, values.Count);

        Assert.Equal("stringKey", values[0].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, values[0].Value.ValueCase);
        Assert.Equal("stringValue", values[0].Value.StringValue);

        Assert.Equal("intKey", values[1].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, values[1].Value.ValueCase);
        Assert.Equal(42L, values[1].Value.IntValue);

        Assert.Equal("boolKey", values[2].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.BoolValue, values[2].Value.ValueCase);
        Assert.True(values[2].Value.BoolValue);

        Assert.Equal("doubleKey", values[3].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.DoubleValue, values[3].Value.ValueCase);
        Assert.Equal(3.14, values[3].Value.DoubleValue);
    }

    [Fact]
    public void KvListWithNullEntryValue()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("nullKey", null),
        };
        var kvp = new KeyValuePair<string, object?>("key", kvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);

        var values = attribute.Value.KvlistValue.Values;
        Assert.Single(values);
        Assert.Equal("nullKey", values[0].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.None, values[0].Value.ValueCase);
    }

    [Fact]
    public void NestedKvList()
    {
        var innerKvList = new List<KeyValuePair<string, object?>>
        {
            new("nestedKey", "nestedValue"),
        };
        var outerKvList = new List<KeyValuePair<string, object?>>
        {
            new("inner", innerKvList),
        };
        var kvp = new KeyValuePair<string, object?>("key", outerKvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);

        var outerValues = attribute.Value.KvlistValue.Values;
        Assert.Single(outerValues);
        Assert.Equal("inner", outerValues[0].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, outerValues[0].Value.ValueCase);

        var innerValues = outerValues[0].Value.KvlistValue.Values;
        Assert.Single(innerValues);
        Assert.Equal("nestedKey", innerValues[0].Key);
        Assert.Equal("nestedValue", innerValues[0].Value.StringValue);
    }

    [Fact]
    public void KvListWithManyEntries()
    {
        var kvList = new List<KeyValuePair<string, object?>>();
        for (int i = 0; i < 50; i++)
        {
            kvList.Add(new($"key{i}", (long)i));
        }

        var kvp = new KeyValuePair<string, object?>("key", kvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);

        var values = attribute.Value.KvlistValue.Values;
        Assert.Equal(50, values.Count);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal($"key{i}", values[i].Key);
            Assert.Equal((long)i, values[i].Value.IntValue);
        }
    }

    [Fact]
    public void DictionaryAsKvList()
    {
        var dict = new Dictionary<string, object?>
        {
            ["alpha"] = "a",
            ["beta"] = 2L,
        };
        var kvp = new KeyValuePair<string, object?>("key", dict);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);
        Assert.Equal(2, attribute.Value.KvlistValue.Values.Count);
    }

    [Fact]
    public void KvListEnumerationFailureDropsTag()
    {
        var kvp = new KeyValuePair<string, object?>("key", FaultyKvList());

        Assert.False(TryTransformTag(kvp, out var attribute));
        Assert.Null(attribute);
    }

    [Fact]
    public void KvListNestedEnumerationFailureDropsEntry()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("key", "value"),
            new("faulty", FaultyKvList()),
            new("intKey", 1),
        };

        var kvp = new KeyValuePair<string, object?>("list", kvList);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.NotNull(attribute);
        Assert.Equal("list", attribute.Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attribute.Value.ValueCase);

        // Faulty entry is dropped
        var values = attribute.Value.KvlistValue.Values;
        Assert.Equal(2, values.Count);

        Assert.Equal("key", values[0].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, values[0].Value.ValueCase);
        Assert.Equal("value", values[0].Value.StringValue);

        Assert.Equal("intKey", values[1].Key);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, values[1].Value.ValueCase);
        Assert.Equal(1, values[1].Value.IntValue);
    }

    [Fact]
    public void KvListAttributeTriggersMainBufferResize()
    {
        var largeString = new string('a', 500_000);
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("large", largeString),
        };

        var tags = new ActivityTagsCollection
        {
            new("kvList", kvList),
        };

        using var activitySource = new ActivitySource(nameof(this.KvListAttributeTriggersMainBufferResize));
        using var activity = activitySource.StartActivity("test", ActivityKind.Server, default(ActivityContext), tags);

        Assert.NotNull(activity);

        var buffer = new byte[4096];
        int writePosition;
        while (true)
        {
            try
            {
                writePosition = ProtobufOtlpTraceSerializer.WriteSpan(buffer, 0, new SdkLimitOptions(), activity);
                break;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                if (!ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Traces))
                {
                    throw;
                }
            }
        }

        Assert.True(buffer.Length > 4096);

        using var stream = new MemoryStream(buffer, 0, writePosition);
        var scopeSpans = OtlpTrace.ScopeSpans.Parser.ParseFrom(stream);
        var span = scopeSpans.Spans.FirstOrDefault();

        Assert.NotNull(span);

        var kvListAttr = span.Attributes.FirstOrDefault(a => a.Key == "kvList");
        Assert.NotNull(kvListAttr);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, kvListAttr.Value.ValueCase);

        var values = kvListAttr.Value.KvlistValue.Values;
        Assert.Single(values);

        Assert.Equal("large", values[0].Key);
        Assert.Equal(largeString, values[0].Value.StringValue);
    }

    [Fact]
    public void RecursionHasMaxDepthAndRecursionDepthIsReset()
    {
        var tags = new ActivityTagsCollection
        {
            new("kvList", SelfReferencingKvList()),
            new("kvList1", SelfReferencingKvList()),
        };

        using var activitySource = new ActivitySource(nameof(this.RecursionHasMaxDepthAndRecursionDepthIsReset));
        using var activity = activitySource.StartActivity("test", ActivityKind.Server, default(ActivityContext), tags);

        Assert.NotNull(activity);

        var buffer = new byte[1_000_000];

        var writePosition = ProtobufOtlpTraceSerializer.WriteSpan(buffer, 0, new SdkLimitOptions(), activity);

        using var stream = new MemoryStream(buffer, 0, writePosition);
        var scopeSpans = OtlpTrace.ScopeSpans.Parser.ParseFrom(stream);
        Assert.Single(scopeSpans.Spans);
        var span = scopeSpans.Spans.FirstOrDefault();

        Assert.NotNull(span);
        Assert.Equal(2, span.Attributes.Count);

        var attribute = span.Attributes[0];
        Assert.Equal("kvList", attribute.Key);

        var attributeValue = attribute.Value;
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attributeValue.ValueCase);
            Assert.Equal(2, attributeValue.KvlistValue.Values.Count);

            Assert.Equal("int", attributeValue.KvlistValue.Values[0].Key);
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, attributeValue.KvlistValue.Values[0].Value.ValueCase);

            Assert.Equal("self", attributeValue.KvlistValue.Values[1].Key);
            if (i < 2)
            {
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attributeValue.KvlistValue.Values[1].Value.ValueCase);
                attributeValue = attributeValue.KvlistValue.Values[1].Value;
                continue;
            }

            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attributeValue.KvlistValue.Values[1].Value.ValueCase);
            Assert.Equal(Convert.ToString(SelfReferencingKvList(), CultureInfo.InvariantCulture), attributeValue.KvlistValue.Values[1].Value.StringValue);
        }

        attribute = span.Attributes[1];
        Assert.Equal("kvList1", attribute.Key);

        attributeValue = attribute.Value;
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attributeValue.ValueCase);
            Assert.Equal(2, attributeValue.KvlistValue.Values.Count);

            Assert.Equal("int", attributeValue.KvlistValue.Values[0].Key);
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, attributeValue.KvlistValue.Values[0].Value.ValueCase);

            Assert.Equal("self", attributeValue.KvlistValue.Values[1].Key);
            if (i < 2)
            {
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.KvlistValue, attributeValue.KvlistValue.Values[1].Value.ValueCase);
                attributeValue = attributeValue.KvlistValue.Values[1].Value;
                continue;
            }

            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attributeValue.KvlistValue.Values[1].Value.ValueCase);
            Assert.Equal(Convert.ToString(SelfReferencingKvList(), CultureInfo.InvariantCulture), attributeValue.KvlistValue.Values[1].Value.StringValue);
        }
    }

    public void Dispose()
    {
        this.activityListener.Dispose();
    }

    private static IEnumerable<KeyValuePair<string, object?>> FaultyKvList()
    {
        yield return new KeyValuePair<string, object?>("key1", "value1");
        throw new InvalidOperationException("simulated failure");
    }

    private static List<KeyValuePair<string, object?>> SelfReferencingKvList()
    {
        var list = new List<KeyValuePair<string, object?>>();
        list.Add(new("int", 1));
        list.Add(new("self", list));
        return list;
    }

    private static bool TryTransformTag(KeyValuePair<string, object?> tag, [NotNullWhen(true)] out OtlpCommon.KeyValue? attribute)
    {
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = new byte[4096],
            WritePosition = 0,
        };

        if (ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag))
        {
            using var stream = new MemoryStream(otlpTagWriterState.Buffer, 0, otlpTagWriterState.WritePosition);
            var keyValue = OtlpCommon.KeyValue.Parser.ParseFrom(stream);
            Assert.NotNull(keyValue);
            attribute = keyValue;
            return true;
        }

        if (otlpTagWriterState.WritePosition > 0)
        {
            using var stream = new MemoryStream(otlpTagWriterState.Buffer, 0, otlpTagWriterState.WritePosition);
            attribute = OtlpCommon.KeyValue.Parser.ParseFrom(stream);
            return false;
        }

        attribute = null;
        return false;
    }
}
