// <copyright file="AttributesExtensions.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    internal static class AttributesExtensions
    {
        public static object GetValue(this IEnumerable<KeyValuePair<string, object>> attributes, string key)
        {
            return attributes.FirstOrDefault(kvp => kvp.Key == key).Value;
        }

        public static void AssertAreSame(
            this IEnumerable<KeyValuePair<string, object>> attributes,
            IEnumerable<KeyValuePair<string, object>> expectedAttributes)
        {
            var expectedKeyValuePairs = expectedAttributes as KeyValuePair<string, object>[] ?? expectedAttributes.ToArray();
            var actualKeyValuePairs = attributes as KeyValuePair<string, object>[] ?? attributes.ToArray();
            Assert.Equal(actualKeyValuePairs.Length, expectedKeyValuePairs.Length);

            foreach (var attr in actualKeyValuePairs)
            {
                Assert.Contains(attr, expectedKeyValuePairs);
            }
        }
    }
}
