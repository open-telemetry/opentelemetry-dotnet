// <copyright file="LogRecordPoolTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Tests.Logs
{
    public sealed class LogRecordPoolTests
    {
        [Fact]
        public void ResizeTests()
        {
            LogRecordPool.Resize(LogRecordPool.DefaultMaxPoolSize);
            Assert.NotNull(LogRecordPool.Current);
            Assert.Equal(LogRecordPool.DefaultMaxPoolSize, LogRecordPool.Current.Capacity);

            Assert.Throws<ArgumentOutOfRangeException>(() => LogRecordPool.Resize(0));

            var beforePool = LogRecordPool.Current;

            LogRecordPool.Resize(1);

            Assert.NotNull(LogRecordPool.Current);
            Assert.Equal(1, LogRecordPool.Current.Capacity);
            Assert.NotEqual(beforePool, LogRecordPool.Current);
        }

        [Fact]
        public void RentReturnTests()
        {
            LogRecordPool.Resize(2);

            var logRecord1 = LogRecordPool.Rent();
            Assert.NotNull(logRecord1);

            var logRecord2 = LogRecordPool.Rent();
            Assert.NotNull(logRecord1);

            LogRecordPool.Return(logRecord1);

            Assert.Equal(1, LogRecordPool.Current.Count);

            // Note: This is ignored because logRecord manually created has PoolReferenceCount = int.MaxValue.
            LogRecord manualRecord = new();
            Assert.Equal(int.MaxValue, manualRecord.PoolReferenceCount);
            LogRecordPool.Return(manualRecord);

            Assert.Equal(1, LogRecordPool.Current.Count);

            LogRecordPool.Return(logRecord2);

            Assert.Equal(2, LogRecordPool.Current.Count);

            logRecord1 = LogRecordPool.Rent();
            Assert.NotNull(logRecord1);
            Assert.Equal(1, LogRecordPool.Current.Count);

            logRecord2 = LogRecordPool.Rent();
            Assert.NotNull(logRecord2);
            Assert.Equal(0, LogRecordPool.Current.Count);

            var logRecord3 = LogRecordPool.Rent();
            var logRecord4 = LogRecordPool.Rent();
            Assert.NotNull(logRecord3);
            Assert.NotNull(logRecord4);

            LogRecordPool.Return(logRecord1);
            LogRecordPool.Return(logRecord2);
            LogRecordPool.Return(logRecord3);
            LogRecordPool.Return(logRecord4); // <- Discarded due to pool size of 2

            Assert.Equal(2, LogRecordPool.Current.Count);
        }

        [Fact]
        public void TrackReferenceTests()
        {
            LogRecordPool.Resize(2);

            var logRecord1 = LogRecordPool.Rent();
            Assert.NotNull(logRecord1);

            Assert.Equal(1, logRecord1.PoolReferenceCount);

            LogRecordPool.TrackReference(logRecord1);

            Assert.Equal(2, logRecord1.PoolReferenceCount);

            LogRecordPool.Return(logRecord1);

            Assert.Equal(1, logRecord1.PoolReferenceCount);

            LogRecordPool.Return(logRecord1);

            Assert.Equal(1, LogRecordPool.Current.Count);
            Assert.Equal(0, logRecord1.PoolReferenceCount);

            LogRecordPool.Return(logRecord1);

            Assert.Equal(-1, logRecord1.PoolReferenceCount);
            Assert.Equal(1, LogRecordPool.Current.Count); // Record was not returned because PoolReferences was negative.
        }

        [Fact]
        public void ClearTests()
        {
            LogRecordPool.Resize(LogRecordPool.DefaultMaxPoolSize);

            var logRecord1 = LogRecordPool.Rent();
            logRecord1.AttributeStorage = new List<KeyValuePair<string, object?>>(16)
            {
                new KeyValuePair<string, object?>("key1", "value1"),
                new KeyValuePair<string, object?>("key2", "value2"),
            };
            logRecord1.BufferedScopes = new List<object?>(8) { null, null };

            LogRecordPool.Return(logRecord1);

            Assert.Empty(logRecord1.AttributeStorage);
            Assert.Equal(16, logRecord1.AttributeStorage.Capacity);
            Assert.Empty(logRecord1.BufferedScopes);
            Assert.Equal(8, logRecord1.BufferedScopes.Capacity);

            logRecord1 = LogRecordPool.Rent();

            Assert.NotNull(logRecord1.AttributeStorage);
            Assert.NotNull(logRecord1.BufferedScopes);

            for (int i = 0; i <= LogRecordPool.DefaultMaxNumberOfAttributes; i++)
            {
                logRecord1.AttributeStorage!.Add(new KeyValuePair<string, object?>("key", "value"));
            }

            for (int i = 0; i <= LogRecordPool.DefaultMaxNumberOfScopes; i++)
            {
                logRecord1.BufferedScopes!.Add(null);
            }

            LogRecordPool.Return(logRecord1);

            Assert.Null(logRecord1.AttributeStorage);
            Assert.Null(logRecord1.BufferedScopes);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExportTest(bool warmup)
        {
            LogRecordPool.Resize(LogRecordPool.DefaultMaxPoolSize);

            if (warmup)
            {
                for (int i = 0; i < LogRecordPool.DefaultMaxPoolSize; i++)
                {
                    LogRecordPool.Return(new LogRecord { PoolReferenceCount = 1 });
                }
            }

            using BatchLogRecordExportProcessor processor = new(new NoopExporter());

            List<Task> tasks = new();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Random random = new Random();

                    await Task.Delay(random.Next(100, 150)).ConfigureAwait(false);

                    for (int i = 0; i < 1000; i++)
                    {
                        var logRecord = LogRecordPool.Rent();

                        processor.OnEnd(logRecord);

                        // This should no-op mostly.
                        LogRecordPool.Return(logRecord);

                        await Task.Delay(random.Next(0, 20)).ConfigureAwait(false);
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            processor.ForceFlush();

            if (warmup)
            {
                Assert.Equal(LogRecordPool.DefaultMaxPoolSize, LogRecordPool.Current.Count);
            }

            Assert.True(LogRecordPool.Current.Count <= LogRecordPool.DefaultMaxPoolSize);
        }

        [Fact]
        public async Task DeadlockTest()
        {
            /*
             * The way the LogRecordPool works is it maintains two counters one
             * for readers and one for writers. The counters always increment
             * and point to an index in the pool array by way of a modulus on
             * the size of the array (index = counter % capacity). Under very
             * heavy load it is possible for a reader to receive an index and
             * then be yielded. When waking up that index may no longer be valid
             * if other threads caused the counters to loop around. There is
             * protection for this case in the pool, this test verifies it is
             * working.
             *
             * This is considered a corner case. Many threads have to be renting
             * & returning logs in a tight loop for this to happen. Real
             * applications should be logging based on logic firing which should
             * have more natural back-off time.
             */

            LogRecordPool.Resize(LogRecordPool.DefaultMaxPoolSize);

            List<Task> tasks = new();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(2000).ConfigureAwait(false);

                    for (int i = 0; i < 100_000; i++)
                    {
                        var logRecord = LogRecordPool.Rent();

                        LogRecordPool.Return(logRecord);
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Assert.True(LogRecordPool.Current.Count <= LogRecordPool.DefaultMaxPoolSize);
        }

        private sealed class NoopExporter : BaseExporter<LogRecord>
        {
            public override ExportResult Export(in Batch<LogRecord> batch)
            {
                return ExportResult.Success;
            }
        }
    }
}
