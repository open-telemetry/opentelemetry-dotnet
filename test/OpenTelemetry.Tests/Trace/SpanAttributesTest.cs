// <copyright file="SpanAttributesTest.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class SpanAttributesTest : IDisposable
    {
        public void Dispose()
        {
            TracerProvider.SpanAttributeCountLimit = null;
            TracerProvider.SpanAttributeValueLengthLimit = null;
        }

        [Fact]
        public void ValidateConstructor()
        {
            var spanAttribute = new SpanAttributes();
            Assert.Empty(spanAttribute.Attributes);
        }

        [Fact]
        public void ValidateAddMethods()
        {
            var spanAttribute = new SpanAttributes();
            spanAttribute.Add("key_string", "string");
            spanAttribute.Add("key_a_string", new string[] { "string" });

            spanAttribute.Add("key_double", 1.01);
            spanAttribute.Add("key_a_double", new double[] { 1.01 });

            spanAttribute.Add("key_bool", true);
            spanAttribute.Add("key_a_bool", new bool[] { true });

            spanAttribute.Add("key_long", 1);
            spanAttribute.Add("key_a_long", new long[] { 1 });

            Assert.Equal(8, spanAttribute.Attributes.Count);
        }

        [Fact]
        public void ValidateNullKey()
        {
            var spanAttribute = new SpanAttributes();
            Assert.Throws<ArgumentNullException>(() => spanAttribute.Add(null, "null key"));
        }

        [Fact]
        public void ValidateSameKey()
        {
            var spanAttribute = new SpanAttributes();
            spanAttribute.Add("key", "value1");
            spanAttribute.Add("key", "value2");
            Assert.Equal("value2", spanAttribute.Attributes["key"]);
        }

        [Fact]
        public void ValidateConstructorWithList()
        {
            var spanAttributes = new SpanAttributes(
               new List<KeyValuePair<string, object>>()
               {
                    new KeyValuePair<string, object>("Span attribute int", 1),
                    new KeyValuePair<string, object>("Span attribute string", "str"),
               });
            Assert.Equal(2, spanAttributes.Attributes.Count);
        }

        [Fact]
        public void ValidateConstructorWithNullList()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanAttributes(null));
        }

        [Fact]
        public void SpanLimitsAddOneAtATime()
        {
            TracerProvider.SetSpanLimits(new SpanLimits
            {
                AttributeCountLimit = 4,
                AttributeValueLengthLimit = 4,
            });

            var spanAttributes = new SpanAttributes();
            spanAttributes.Add("TruncatedStringAttribute", "12345");
            spanAttributes.Add("StringAttribute", "123");

            // There is no Add overload that takes an int. This invokes the Add(string, long) overload.
            spanAttributes.Add("IntAttribute", 12345);
            spanAttributes.Add("StringArrayAttribute", new[] { "ABCDE", "FGHIJ", "KLMN", "OPQ" });
            spanAttributes.Add("DoubleAttribute", 12345.6);

            Assert.Equal(4, spanAttributes.Attributes.Count);
            Assert.Equal("1234", spanAttributes.Attributes["TruncatedStringAttribute"]);
            Assert.Equal("123", spanAttributes.Attributes["StringAttribute"]);
            Assert.Equal(12345L, spanAttributes.Attributes["IntAttribute"]);
            Assert.Equal(new[] { "ABCD", "FGHI", "KLMN", "OPQ" }, spanAttributes.Attributes["StringArrayAttribute"]);
        }

        [Fact]
        public void SpanLimitsAddMultipleAtOnce()
        {
            TracerProvider.SetSpanLimits(new SpanLimits
            {
                AttributeCountLimit = 4,
                AttributeValueLengthLimit = 4,
            });

            var spanAttributes = new SpanAttributes(
               new List<KeyValuePair<string, object>>()
               {
                    new KeyValuePair<string, object>("TruncatedStringAttribute", "12345"),
                    new KeyValuePair<string, object>("StringAttribute", "123"),
                    new KeyValuePair<string, object>("IntAttribute", 12345),
                    new KeyValuePair<string, object>("StringArrayAttribute", new[] { "ABCDE", "FGHIJ", "KLMN", "OPQ" }),
                    new KeyValuePair<string, object>("DoubleAttribute", 12345.6),
               });

            Assert.Equal(4, spanAttributes.Attributes.Count);
            Assert.Equal("1234", spanAttributes.Attributes["TruncatedStringAttribute"]);
            Assert.Equal("123", spanAttributes.Attributes["StringAttribute"]);
            Assert.Equal(12345, spanAttributes.Attributes["IntAttribute"]);
            Assert.Equal(new[] { "ABCD", "FGHI", "KLMN", "OPQ" }, spanAttributes.Attributes["StringArrayAttribute"]);
        }

        [Fact]
        public void SpanLimitsUnset()
        {
            TracerProvider.SetSpanLimits(new SpanLimits
            {
                AttributeCountLimit = null,
                AttributeValueLengthLimit = null,
            });

            var spanAttributes = new SpanAttributes(
               new List<KeyValuePair<string, object>>()
               {
                    new KeyValuePair<string, object>("TruncatedStringAttribute", "12345"),
                    new KeyValuePair<string, object>("StringAttribute", "123"),
                    new KeyValuePair<string, object>("IntAttribute", 12345),
                    new KeyValuePair<string, object>("StringArrayAttribute", new[] { "ABCDE", "FGHIJ", "KLMN", "OPQ" }),
                    new KeyValuePair<string, object>("DoubleAttribute", 12345.6),
               });

            Assert.Equal(5, spanAttributes.Attributes.Count);
            Assert.Equal("12345", spanAttributes.Attributes["TruncatedStringAttribute"]);
            Assert.Equal("123", spanAttributes.Attributes["StringAttribute"]);
            Assert.Equal(12345, spanAttributes.Attributes["IntAttribute"]);
            Assert.Equal(new[] { "ABCDE", "FGHIJ", "KLMN", "OPQ" }, spanAttributes.Attributes["StringArrayAttribute"]);
            Assert.Equal(12345.6, spanAttributes.Attributes["DoubleAttribute"]);
        }

        [Fact]
        public void SpanLimitsIgnoredWhenSetToInvalidValue()
        {
            TracerProvider.SetSpanLimits(new SpanLimits
            {
                AttributeCountLimit = -5,
                AttributeValueLengthLimit = -5,
            });

            var spanAttributes = new SpanAttributes(
               new List<KeyValuePair<string, object>>()
               {
                    new KeyValuePair<string, object>("TruncatedStringAttribute", "12345"),
                    new KeyValuePair<string, object>("StringAttribute", "123"),
                    new KeyValuePair<string, object>("IntAttribute", 12345),
                    new KeyValuePair<string, object>("StringArrayAttribute", new[] { "ABCDE", "FGHIJ", "KLMN", "OPQ" }),
                    new KeyValuePair<string, object>("DoubleAttribute", 12345.6),
               });

            Assert.Equal(5, spanAttributes.Attributes.Count);
            Assert.Equal("12345", spanAttributes.Attributes["TruncatedStringAttribute"]);
            Assert.Equal("123", spanAttributes.Attributes["StringAttribute"]);
            Assert.Equal(12345, spanAttributes.Attributes["IntAttribute"]);
            Assert.Equal(new[] { "ABCDE", "FGHIJ", "KLMN", "OPQ" }, spanAttributes.Attributes["StringArrayAttribute"]);
            Assert.Equal(12345.6, spanAttributes.Attributes["DoubleAttribute"]);
        }
    }
}
