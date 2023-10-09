// <copyright file="OtlpAttributeTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using Xunit;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpAttributeTests
{
    [Fact]
    public void NullValueAttribute()
    {
        var kvp = new KeyValuePair<string, object>("key", null);
        Assert.False(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var _));
    }

    [Fact]
    public void EmptyArrays()
    {
        var kvp = new KeyValuePair<string, object>("key", Array.Empty<int>());
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
        Assert.Empty(attribute.Value.ArrayValue.Values);

        kvp = new KeyValuePair<string, object>("key", Array.Empty<object>());
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out attribute));
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
        var kvp = new KeyValuePair<string, object>("key", value);
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));

        switch (value)
        {
            case Array array:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
                var expectedArray = new long[array.Length];
                for (var i = 0; i < array.Length; i++)
                {
                    expectedArray[i] = Convert.ToInt64(array.GetValue(i));
                }

                Assert.Equal(expectedArray, attribute.Value.ArrayValue.Values.Select(x => x.IntValue));
                break;
            default:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.IntValue, attribute.Value.ValueCase);
                Assert.Equal(Convert.ToInt64(value), attribute.Value.IntValue);
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
        var kvp = new KeyValuePair<string, object>("key", value);
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));

        switch (value)
        {
            case Array array:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
                var expectedArray = new double[array.Length];
                for (var i = 0; i < array.Length; i++)
                {
                    expectedArray[i] = Convert.ToDouble(array.GetValue(i));
                }

                Assert.Equal(expectedArray, attribute.Value.ArrayValue.Values.Select(x => x.DoubleValue));
                break;
            default:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.DoubleValue, attribute.Value.ValueCase);
                Assert.Equal(Convert.ToDouble(value), attribute.Value.DoubleValue);
                break;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(new bool[] { true, false, true })]
    public void BooleanTypeSupported(object value)
    {
        var kvp = new KeyValuePair<string, object>("key", value);
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));

        switch (value)
        {
            case Array array:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
                var expectedArray = new bool[array.Length];
                for (var i = 0; i < array.Length; i++)
                {
                    expectedArray[i] = Convert.ToBoolean(array.GetValue(i));
                }

                Assert.Equal(expectedArray, attribute.Value.ArrayValue.Values.Select(x => x.BoolValue));
                break;
            default:
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.BoolValue, attribute.Value.ValueCase);
                Assert.Equal(Convert.ToBoolean(value), attribute.Value.BoolValue);
                break;
        }
    }

    [Theory]
    [InlineData(char.MaxValue)]
    [InlineData("string")]
    public void StringTypesSupported(object value)
    {
        var kvp = new KeyValuePair<string, object>("key", value);
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ValueCase);
        Assert.Equal(Convert.ToString(value), attribute.Value.StringValue);
    }

    [Fact]
    public void StringArrayTypesSupported()
    {
        var charArray = new char[] { 'a', 'b', 'c' };
        var stringArray = new string[] { "a", "b", "c", string.Empty, null };

        var kvp = new KeyValuePair<string, object>("key", charArray);
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);
        Assert.Equal(charArray.Select(x => x.ToString()), attribute.Value.ArrayValue.Values.Select(x => x.StringValue));

        kvp = new KeyValuePair<string, object>("key", stringArray);
        Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out attribute));
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
            new object(),
        };

        var testArrayValues = new object[]
        {
            new nint[] { 1, 2, 3 },
            new nuint[] { 1, 2, 3 },
            new decimal[] { 1, 2, 3 },
            new object[] { 1, new object(), false, null },
        };

        foreach (var value in testValues)
        {
            var kvp = new KeyValuePair<string, object>("key", value);
            Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.StringValue, attribute.Value.ValueCase);
            Assert.Equal(value.ToString(), attribute.Value.StringValue);
        }

        foreach (var value in testArrayValues)
        {
            var kvp = new KeyValuePair<string, object>("key", value);
            Assert.True(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var attribute));
            Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, attribute.Value.ValueCase);

            var array = value as Array;
            for (var i = 0; i < attribute.Value.ArrayValue.Values.Count; ++i)
            {
                var expectedValue = array.GetValue(i)?.ToString();
                var expectedValueCase = expectedValue != null
                    ? OtlpCommon.AnyValue.ValueOneofCase.StringValue
                    : OtlpCommon.AnyValue.ValueOneofCase.None;

                Assert.Equal(expectedValueCase, attribute.Value.ArrayValue.Values[i].ValueCase);
                if (expectedValueCase != OtlpCommon.AnyValue.ValueOneofCase.None)
                {
                    Assert.Equal(array.GetValue(i).ToString(), attribute.Value.ArrayValue.Values[i].StringValue);
                }
            }
        }
    }

    [Fact]
    public void ExceptionInToStringIsCaught()
    {
        var kvp = new KeyValuePair<string, object>("key", new MyToStringMethodThrowsAnException());
        Assert.False(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var _));

        kvp = new KeyValuePair<string, object>("key", new object[] { 1, false, new MyToStringMethodThrowsAnException() });
        Assert.False(OtlpKeyValueTransformer.Instance.TryTransformTag(kvp, out var _));
    }

    private class MyToStringMethodThrowsAnException
    {
        public override string ToString()
        {
            throw new Exception("Nope.");
        }
    }
}
