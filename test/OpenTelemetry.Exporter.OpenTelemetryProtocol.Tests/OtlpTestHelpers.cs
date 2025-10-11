// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Google.Protobuf.Collections;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal static class OtlpTestHelpers
{
    public static void AssertOtlpAttributes(
        IEnumerable<KeyValuePair<string, object?>> expected,
        RepeatedField<OtlpCommon.KeyValue> actual)
    {
        var expectedAttributes = expected.ToList();
        int expectedSize = 0;
        for (int i = 0; i < expectedAttributes.Count; i++)
        {
            var current = expectedAttributes[i].Value;
            Assert.Equal(expectedAttributes[i].Key, actual[i].Key);
            Assert.NotNull(current);

            if (current.GetType().IsArray)
            {
                Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.ArrayValue, actual[i].Value.ValueCase);
                if (current is bool[] boolArray)
                {
                    Assert.Equal(boolArray.Length, actual[i].Value.ArrayValue.Values.Count);
                    for (var j = 0; j < boolArray.Length; ++j)
                    {
                        AssertOtlpAttributeValue(boolArray[j], actual[i].Value.ArrayValue.Values[j]);
                    }

                    expectedSize++;
                }
                else if (current is int[] intArray)
                {
                    Assert.Equal(intArray.Length, actual[i].Value.ArrayValue.Values.Count);
                    for (var j = 0; j < intArray.Length; ++j)
                    {
                        AssertOtlpAttributeValue(intArray[j], actual[i].Value.ArrayValue.Values[j]);
                    }

                    expectedSize++;
                }
                else if (current is double[] doubleArray)
                {
                    Assert.Equal(doubleArray.Length, actual[i].Value.ArrayValue.Values.Count);
                    for (var j = 0; j < doubleArray.Length; ++j)
                    {
                        AssertOtlpAttributeValue(doubleArray[j], actual[i].Value.ArrayValue.Values[j]);
                    }

                    expectedSize++;
                }
                else if (current is string[] stringArray)
                {
                    Assert.Equal(stringArray.Length, actual[i].Value.ArrayValue.Values.Count);
                    for (var j = 0; j < stringArray.Length; ++j)
                    {
                        AssertOtlpAttributeValue(stringArray[j], actual[i].Value.ArrayValue.Values[j]);
                    }

                    expectedSize++;
                }
                else
                {
                    var source = (Array)current;

                    Assert.Equal(source.Length, actual[i].Value.ArrayValue.Values.Count);

                    for (int j = 0; j < source.Length; j++)
                    {
                        var item = source.GetValue(j);

                        AssertOtlpAttributeValue(item, actual[i].Value.ArrayValue.Values[j]);
                    }

                    expectedSize++;
                }
            }
            else
            {
                Assert.Equal(expectedAttributes[i].Key, actual[i].Key);
                AssertOtlpAttributeValue(current, actual[i].Value);
                expectedSize++;
            }
        }

        Assert.Equal(expectedSize, actual.Count);
    }

    private static void AssertOtlpAttributeValue(object? expected, OtlpCommon.AnyValue actual)
    {
        switch (expected)
        {
            case string s:
                Assert.Equal(s, actual.StringValue);
                break;
            case bool b:
                Assert.Equal(b, actual.BoolValue);
                break;
            case long l:
                Assert.Equal(l, actual.IntValue);
                break;
            case double d:
                Assert.Equal(d, actual.DoubleValue);
                break;
            case int i:
                Assert.Equal(i, actual.IntValue);
                break;
            default:
                Assert.Equal(Convert.ToString(expected, CultureInfo.InvariantCulture), actual.StringValue);
                break;
        }
    }
}
