// <copyright file="LogRecordThreadStaticPoolTests.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LogRecordThreadStaticPoolTests
    {
        [Fact]
        public void RentReturnTests()
        {
            LogRecordThreadStaticPool.Storage = null;

            var logRecord = LogRecordThreadStaticPool.Instance.Rent();
            Assert.NotNull(logRecord);
            Assert.Null(LogRecordThreadStaticPool.Storage);

            LogRecordThreadStaticPool.Instance.Return(logRecord);
            Assert.NotNull(LogRecordThreadStaticPool.Storage);
            Assert.Equal(logRecord, LogRecordThreadStaticPool.Storage);

            LogRecordThreadStaticPool.Instance.Return(new());
            Assert.NotNull(LogRecordThreadStaticPool.Storage);
            Assert.Equal(logRecord, LogRecordThreadStaticPool.Storage);

            LogRecordThreadStaticPool.Storage = null;

            var manual = new LogRecord();
            LogRecordThreadStaticPool.Instance.Return(manual);
            Assert.NotNull(LogRecordThreadStaticPool.Storage);
            Assert.Equal(manual, LogRecordThreadStaticPool.Storage);
        }

        [Fact]
        public void ClearTests()
        {
            var logRecord1 = LogRecordThreadStaticPool.Instance.Rent();
            logRecord1.AttributeStorage = new List<KeyValuePair<string, object?>>(16)
            {
                new KeyValuePair<string, object?>("key1", "value1"),
                new KeyValuePair<string, object?>("key2", "value2"),
            };
            logRecord1.ScopeStorage = new List<object?>(8) { null, null };

            LogRecordThreadStaticPool.Instance.Return(logRecord1);

            Assert.Empty(logRecord1.AttributeStorage);
            Assert.Equal(16, logRecord1.AttributeStorage.Capacity);
            Assert.Empty(logRecord1.ScopeStorage);
            Assert.Equal(8, logRecord1.ScopeStorage.Capacity);

            logRecord1 = LogRecordThreadStaticPool.Instance.Rent();

            Assert.NotNull(logRecord1.AttributeStorage);
            Assert.NotNull(logRecord1.ScopeStorage);

            for (int i = 0; i <= LogRecordPoolHelper.DefaultMaxNumberOfAttributes; i++)
            {
                logRecord1.AttributeStorage!.Add(new KeyValuePair<string, object?>("key", "value"));
            }

            for (int i = 0; i <= LogRecordPoolHelper.DefaultMaxNumberOfScopes; i++)
            {
                logRecord1.ScopeStorage!.Add(null);
            }

            LogRecordThreadStaticPool.Instance.Return(logRecord1);

            Assert.Null(logRecord1.AttributeStorage);
            Assert.Null(logRecord1.ScopeStorage);
        }
    }
}
