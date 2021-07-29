// <copyright file="OtlpTestHelpers.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Xunit;
using OtlpCommon = Opentelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    internal static class OtlpTestHelpers
    {
        public static void AssertOtlpAttributes(
            IEnumerable<KeyValuePair<string, object>> expected,
            RepeatedField<OtlpCommon.KeyValue> actual)
        {
            var expectedAttributes = expected.ToList();
            int expectedSize = 0;
            for (int i = 0; i < expectedAttributes.Count; i++)
            {
                var current = expectedAttributes[i].Value;

                if (current.GetType().IsArray)
                {
                    if (current is bool[] boolArray)
                    {
                        int index = 0;
                        foreach (var item in boolArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, actual[i + index].Key);
                            AssertOtlpAttributeValue(item, actual[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                    else if (current is int[] intArray)
                    {
                        int index = 1;
                        foreach (var item in intArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, actual[i + index].Key);
                            AssertOtlpAttributeValue(item, actual[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                    else if (current is double[] doubleArray)
                    {
                        int index = 2;
                        foreach (var item in doubleArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, actual[i + index].Key);
                            AssertOtlpAttributeValue(item, actual[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                    else if (current is string[] stringArray)
                    {
                        int index = 3;
                        foreach (var item in stringArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, actual[i + index].Key);
                            AssertOtlpAttributeValue(item, actual[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                }
                else
                {
                    Assert.Equal(expectedAttributes[i].Key, actual[i].Key);
                    AssertOtlpAttributeValue(current, actual[i]);
                    expectedSize++;
                }
            }

            Assert.Equal(expectedSize, actual.Count);
        }

        private static void AssertOtlpAttributeValue(object expected, OtlpCommon.KeyValue actual)
        {
            switch (expected)
            {
                case string s:
                    Assert.Equal(s, actual.Value.StringValue);
                    break;
                case bool b:
                    Assert.Equal(b, actual.Value.BoolValue);
                    break;
                case long l:
                    Assert.Equal(l, actual.Value.IntValue);
                    break;
                case double d:
                    Assert.Equal(d, actual.Value.DoubleValue);
                    break;
                case int i:
                    Assert.Equal(i, actual.Value.IntValue);
                    break;
                default:
                    Assert.Equal(expected.ToString(), actual.Value.StringValue);
                    break;
            }
        }
    }
}
