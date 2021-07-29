// <copyright file="ZipkinActivityConversionExtensionsTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using Xunit;
using static OpenTelemetry.Exporter.Zipkin.Implementation.ZipkinActivityConversionExtensions;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests
{
    public class ZipkinActivityConversionExtensionsTest
    {
        [Theory]
        [InlineData("int", 1)]
        [InlineData("string", "s")]
        [InlineData("bool", true)]
        [InlineData("double", 1.0)]
        public void CheckProcessTag(string key, object value)
        {
            var attributeEnumerationState = new TagEnumerationState
            {
                Tags = PooledList<KeyValuePair<string, object>>.Create(),
            };

            attributeEnumerationState.ForEach(new KeyValuePair<string, object>(key, value));

            Assert.Equal(key, attributeEnumerationState.Tags[0].Key);
            Assert.Equal(value, attributeEnumerationState.Tags[0].Value);
        }

        [Theory]
        [InlineData("int", null)]
        [InlineData("string", null)]
        [InlineData("bool", null)]
        [InlineData("double", null)]
        public void CheckNullValueProcessTag(string key, object value)
        {
            var attributeEnumerationState = new TagEnumerationState
            {
                Tags = PooledList<KeyValuePair<string, object>>.Create(),
            };

            attributeEnumerationState.ForEach(new KeyValuePair<string, object>(key, value));

            Assert.Empty(attributeEnumerationState.Tags);
        }
    }
}
