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

namespace OpenTelemetry.Trace.Test
{
    public class SpanAttributesTest
    {
        [Fact]
        public void ValidateNullParameter()
        {
            var spanAttribute = new SpanAttributes(null);
            Assert.Empty(spanAttribute.Attributes);
        }

        [Fact]
        public void ValidateEmptyParameter()
        {
            var spanAttribute = new SpanAttributes(new Dictionary<string, object>());
            Assert.Empty(spanAttribute.Attributes);
        }

        [Fact]
        public void ValidateDictionaryParameter()
        {
            var spanAttribute = new SpanAttributes(new Dictionary<string, object> { { "key", "value" } });
            Assert.Single(spanAttribute.Attributes);
            Assert.Equal("value", spanAttribute.Attributes["key"]);
        }

        [Fact]
        public void ValidateAddMethods()
        {
            var spanAttribute = new SpanAttributes(null);
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
        public void ValidateSameKey()
        {
            var spanAttribute = new SpanAttributes(null);
            spanAttribute.Add("key", "value1");
            spanAttribute.Add("key", "value2");
            Assert.Equal("value2", spanAttribute.Attributes["key"]);
        }
    }
}
