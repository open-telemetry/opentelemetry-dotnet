// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using Xunit;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpAttributeTests
{
    [Fact]
    public void NullValueAttribute()
    {
        var kvp = new KeyValuePair<string, object?>("key", null);
        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.None, attribute.Value.ValueCase);
        Assert.False(attribute.Value.HasBoolValue);
        Assert.False(attribute.Value.HasBytesValue);
        Assert.False(attribute.Value.HasDoubleValue);
        Assert.False(attribute.Value.HasIntValue);
        Assert.False(attribute.Value.HasStringValue);
    }

    [Fact]
    public void EmptyArrays()
    {
        var kvp = new KeyValuePair<string, object?>("key", Array.Empty<int>());
        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
        Assert.Empty(attribute.Value.ArrayValue.Values);

        kvp = new KeyValuePair<string, object?>("key", Array.Empty<object>());
        Assert.True(TryTransformTag(kvp, out attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
        Assert.Empty(attribute.Value.ArrayValue.Values);
    }

    [Theory]
    [InlineData(sbyte.MaxValue)]
    [InlineData(byte.MaxValue)]
    [InlineData(short.MaxValue)]
    [InlineData(ushort.MaxValue)]
    [InlineData(int.MaxValue)]
    [InlineData(uint.MaxValue)]
    [InlineData(long.MaxValue)]
    [InlineData(new sbyte[] { 1, 2, 3 })]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new short[] { 1, 2, 3 })]
    [InlineData(new ushort[] { 1, 2, 3 })]
    [InlineData(new int[] { 1, 2, 3 })]
    [InlineData(new uint[] { 1, 2, 3 })]
    [InlineData(new long[] { 1, 2, 3 })]
    public void IntegralTypesSupported(object value)
    {
        var kvp = new KeyValuePair<string, object?>("key", value);
        Assert.True(TryTransformTag(kvp, out var attribute));

        switch (value)
        {
            case Array array:
                if (array is byte[] byteArray)
                {
                    Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.BytesValue, attribute.Value.ValueCase);
                    Assert.Equal(byteArray.Length, attribute.Value.BytesValue.Length);
                    Assert.Equal(byteArray, attribute.Value.BytesValue.ToByteArray());
                }
                else
                {
                    Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
                    var expectedArray = new long[array.Length];
                    for (var i = 0; i < array.Length; i++)
                    {
                        expectedArray[i] = Convert.ToInt64(array.GetValue(i), CultureInfo.InvariantCulture);
                    }

                    Assert.Equal(expectedArray, attribute.Value.ArrayValue.Values.Select(x => x.IntValue));
                }

                break;
            default:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, attribute.Value.ValueCase);
                Assert.Equal(Convert.ToInt64(value, CultureInfo.InvariantCulture), attribute.Value.IntValue);
                break;
        }
    }

    [Theory]
    [InlineData(float.MaxValue)]
    [InlineData(double.MaxValue)]
    [InlineData(new float[] { 1, 2, 3 })]
    [InlineData(new double[] { 1, 2, 3 })]
    public void FloatingPointTypesSupported(object value)
    {
        var kvp = new KeyValuePair<string, object?>("key", value);
        Assert.True(TryTransformTag(kvp, out var attribute));

        switch (value)
        {
            case Array array:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
                var expectedArray = new double[array.Length];
                for (var i = 0; i < array.Length; i++)
                {
                    expectedArray[i] = Convert.ToDouble(array.GetValue(i), CultureInfo.InvariantCulture);
                }

                Assert.Equal(expectedArray, attribute.Value.ArrayValue.Values.Select(x => x.DoubleValue));
                break;
            default:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.DoubleValue, attribute.Value.ValueCase);
                Assert.Equal(Convert.ToDouble(value, CultureInfo.InvariantCulture), attribute.Value.DoubleValue);
                break;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(new bool[] { true, false, true })]
    public void BooleanTypeSupported(object value)
    {
        var kvp = new KeyValuePair<string, object?>("key", value);
        Assert.True(TryTransformTag(kvp, out var attribute));

        switch (value)
        {
            case Array array:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
                var expectedArray = new bool[array.Length];
                for (var i = 0; i < array.Length; i++)
                {
                    expectedArray[i] = Convert.ToBoolean(array.GetValue(i), CultureInfo.InvariantCulture);
                }

                Assert.Equal(expectedArray, attribute.Value.ArrayValue.Values.Select(x => x.BoolValue));
                break;
            default:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.BoolValue, attribute.Value.ValueCase);
                Assert.Equal(Convert.ToBoolean(value, CultureInfo.InvariantCulture), attribute.Value.BoolValue);
                break;
        }
    }

    [Theory]
    [InlineData(char.MaxValue)]
    [InlineData("string")]
    public void StringTypesSupported(object value)
    {
        var kvp = new KeyValuePair<string, object?>("key", value);
        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ValueCase);
        Assert.Equal(Convert.ToString(value, CultureInfo.InvariantCulture), attribute.Value.StringValue);
    }

    [Fact]
    public void ObjectArrayTypesSupported()
    {
        var obj = new object();
        var objectArray = new object?[] { null, "a", 'b', true, int.MaxValue, long.MaxValue, float.MaxValue, double.MaxValue, obj };

        var kvp = new KeyValuePair<string, object?>("key", objectArray);

        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.None, attribute.Value.ArrayValue.Values[0].ValueCase);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ArrayValue.Values[1].ValueCase);
        Assert.Equal("a", attribute.Value.ArrayValue.Values[1].StringValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ArrayValue.Values[2].ValueCase);
        Assert.Equal("b", attribute.Value.ArrayValue.Values[2].StringValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.BoolValue, attribute.Value.ArrayValue.Values[3].ValueCase);
        Assert.True(attribute.Value.ArrayValue.Values[3].BoolValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, attribute.Value.ArrayValue.Values[4].ValueCase);
        Assert.Equal(int.MaxValue, attribute.Value.ArrayValue.Values[4].IntValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, attribute.Value.ArrayValue.Values[5].ValueCase);
        Assert.Equal(long.MaxValue, attribute.Value.ArrayValue.Values[5].IntValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.DoubleValue, attribute.Value.ArrayValue.Values[6].ValueCase);
        Assert.Equal(float.MaxValue, attribute.Value.ArrayValue.Values[6].DoubleValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.DoubleValue, attribute.Value.ArrayValue.Values[7].ValueCase);
        Assert.Equal(double.MaxValue, attribute.Value.ArrayValue.Values[7].DoubleValue);

        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ArrayValue.Values[8].ValueCase);
        Assert.Equal(obj.ToString(), attribute.Value.ArrayValue.Values[8].StringValue);
    }

    [Fact]
    public void StringArrayTypesSupported()
    {
        var charArray = new char[] { 'a', 'b', 'c' };
        var stringArray = new string?[] { "a", "b", "c", string.Empty, null };

        var kvp = new KeyValuePair<string, object?>("key", charArray);
        Assert.True(TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
        Assert.Equal(charArray.Select(x => x.ToString()), attribute.Value.ArrayValue.Values.Select(x => x.StringValue));

        kvp = new KeyValuePair<string, object?>("key", stringArray);
        Assert.True(TryTransformTag(kvp, out attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);

        for (var i = 0; i < stringArray.Length; ++i)
        {
            var expectedValue = stringArray[i];
            var expectedValueCase = expectedValue != null
                ? OtlpCommon.AnyValue.ValueOneofCase.StringValue
                : OtlpCommon.AnyValue.ValueOneofCase.None;

            Assert.Equal(expectedValueCase, attribute.Value.ArrayValue.Values[i].ValueCase);
            if (expectedValueCase != OtlpCommon.AnyValue.ValueOneofCase.None)
            {
                Assert.Equal(expectedValue, attribute.Value.ArrayValue.Values[i].StringValue);
            }
        }
    }

    [Fact]
    public void ToStringIsCalledForAllOtherTypes()
    {
        var testValues = new object[]
        {
            (nint)int.MaxValue,
            (nuint)uint.MaxValue,
            decimal.MaxValue,
            new(),
        };

        var testArrayValues = new object[]
        {
            new nint[] { 1, 2, 3 },
            new nuint[] { 1, 2, 3 },
            new decimal[] { 1, 2, 3 },
            new object?[] { new object[3], new(), null },
        };

        foreach (var value in testValues)
        {
            var kvp = new KeyValuePair<string, object?>("key", value);
            Assert.True(TryTransformTag(kvp, out var attribute));
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ValueCase);
            Assert.Equal(value.ToString(), attribute.Value.StringValue);
        }

        foreach (var value in testArrayValues)
        {
            var kvp = new KeyValuePair<string, object?>("key", value);
            Assert.True(TryTransformTag(kvp, out var attribute));
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);

            var array = value as Array;
            Assert.NotNull(array);
            for (var i = 0; i < attribute.Value.ArrayValue.Values.Count; ++i)
            {
                var expectedValue = array.GetValue(i)?.ToString();
                var expectedValueCase = expectedValue != null
                    ? OtlpCommon.AnyValue.ValueOneofCase.StringValue
                    : OtlpCommon.AnyValue.ValueOneofCase.None;

                Assert.Equal(expectedValueCase, attribute.Value.ArrayValue.Values[i].ValueCase);
                if (expectedValueCase != OtlpCommon.AnyValue.ValueOneofCase.None)
                {
                    Assert.Equal(array.GetValue(i)!.ToString(), attribute.Value.ArrayValue.Values[i].StringValue);
                }
            }
        }
    }

    [Fact]
    public void ExceptionInToStringIsCaught()
    {
        var kvp = new KeyValuePair<string, object?>("key", new MyToStringMethodThrowsAnException());
        Assert.False(TryTransformTag(kvp, out _));

        kvp = new KeyValuePair<string, object?>("key", new object[] { 1, false, new MyToStringMethodThrowsAnException() });
        Assert.False(TryTransformTag(kvp, out _));
    }

    private static bool TryTransformTag(KeyValuePair<string, object?> tag, [NotNullWhen(true)] out OtlpCommon.KeyValue? attribute)
    {
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = new byte[1024],
            WritePosition = 0,
        };

        if (ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag))
        {
            // Deserialize the ResourceSpans and validate the attributes.
            using (var stream = new MemoryStream(otlpTagWriterState.Buffer, 0, otlpTagWriterState.WritePosition))
            {
                var keyValue = OtlpCommon.KeyValue.Parser.ParseFrom(stream);
                Assert.NotNull(keyValue);
                attribute = keyValue;
            }

            return true;
        }

        attribute = null;
        return false;
    }

    private sealed class MyToStringMethodThrowsAnException
    {
        public override string ToString()
        {
            throw new InvalidOperationException("Nope.");
        }
    }
}
