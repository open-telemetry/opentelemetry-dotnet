// <copyright file="LogRecordAttributeListTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LogRecordAttributeListTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(64)]
        public void ReadWriteTest(int numberOfItems)
        {
            LogRecordAttributeList attributes = default;

            for (int i = 0; i < numberOfItems; i++)
            {
                attributes.Add($"key{i}", i);
            }

            Assert.Equal(numberOfItems, attributes.Count);

            for (int i = 0; i < numberOfItems; i++)
            {
                var item = attributes[i];

                Assert.Equal($"key{i}", item.Key);
                Assert.Equal(i, (int)item.Value);
            }

            int index = 0;
            foreach (KeyValuePair<string, object> item in attributes)
            {
                Assert.Equal($"key{index}", item.Key);
                Assert.Equal(index, (int)item.Value);
                index++;
            }

            if (attributes.Count <= LogRecordAttributeList.OverflowAdditionalCapacity)
            {
                Assert.Null(attributes.OverflowAttributes);
            }
            else
            {
                Assert.NotNull(attributes.OverflowAttributes);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(64)]
        public void ApplyToLogRecordTest(int numberOfItems)
        {
            LogRecordAttributeList attributes = default;

            for (int i = 0; i < numberOfItems; i++)
            {
                attributes.Add($"key{i}", i);
            }

            LogRecord logRecord = new();

            attributes.ApplyToLogRecord(logRecord);

            if (numberOfItems == 0)
            {
                Assert.Null(logRecord.StateValues);
                return;
            }

            Assert.NotNull(logRecord.StateValues);

            int index = 0;
            foreach (KeyValuePair<string, object> item in logRecord.StateValues)
            {
                Assert.Equal($"key{index}", item.Key);
                Assert.Equal(index, (int)item.Value);
                index++;
            }
        }
    }
}
