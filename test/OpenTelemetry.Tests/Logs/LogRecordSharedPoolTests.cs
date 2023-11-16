// <copyright file="LogRecordSharedPoolTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordSharedPoolTests
{
    [Fact]
    public void ResizeTests()
    {
        LogRecordSharedPool.Resize(LogRecordSharedPool.DefaultMaxPoolSize);
        Assert.NotNull(LogRecordSharedPool.Current);
        Assert.Equal(LogRecordSharedPool.DefaultMaxPoolSize, LogRecordSharedPool.Current.Capacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => LogRecordSharedPool.Resize(0));

        var beforePool = LogRecordSharedPool.Current;

        LogRecordSharedPool.Resize(1);

        Assert.NotNull(LogRecordSharedPool.Current);
        Assert.Equal(1, LogRecordSharedPool.Current.Capacity);
        Assert.NotEqual(beforePool, LogRecordSharedPool.Current);
    }

    [Fact]
    public void RentReturnTests()
    {
        LogRecordSharedPool.Resize(2);

        var pool = LogRecordSharedPool.Current;

        var logRecord1 = pool.Rent();
        Assert.NotNull(logRecord1);

        var logRecord2 = pool.Rent();
        Assert.NotNull(logRecord1);

        pool.Return(logRecord1);

        Assert.Equal(1, pool.Count);

        // Note: This is ignored because logRecord manually created has PoolReferenceCount = int.MaxValue.
        LogRecord manualRecord = new();
        Assert.Equal(int.MaxValue, manualRecord.PoolReferenceCount);
        pool.Return(manualRecord);

        Assert.Equal(1, pool.Count);

        pool.Return(logRecord2);

        Assert.Equal(2, pool.Count);

        logRecord1 = pool.Rent();
        Assert.NotNull(logRecord1);
        Assert.Equal(1, pool.Count);

        logRecord2 = pool.Rent();
        Assert.NotNull(logRecord2);
        Assert.Equal(0, pool.Count);

        var logRecord3 = pool.Rent();
        var logRecord4 = pool.Rent();
        Assert.NotNull(logRecord3);
        Assert.NotNull(logRecord4);

        pool.Return(logRecord1);
        pool.Return(logRecord2);
        pool.Return(logRecord3);
        pool.Return(logRecord4); // <- Discarded due to pool size of 2

        Assert.Equal(2, pool.Count);
    }

    [Fact]
    public void TrackReferenceTests()
    {
        LogRecordSharedPool.Resize(2);

        var pool = LogRecordSharedPool.Current;

        var logRecord1 = pool.Rent();
        Assert.NotNull(logRecord1);

        Assert.Equal(1, logRecord1.PoolReferenceCount);

        logRecord1.AddReference();

        Assert.Equal(2, logRecord1.PoolReferenceCount);

        pool.Return(logRecord1);

        Assert.Equal(1, logRecord1.PoolReferenceCount);

        pool.Return(logRecord1);

        Assert.Equal(1, pool.Count);
        Assert.Equal(0, logRecord1.PoolReferenceCount);

        pool.Return(logRecord1);

        Assert.Equal(-1, logRecord1.PoolReferenceCount);
        Assert.Equal(1, pool.Count); // Record was not returned because PoolReferences was negative.
    }

    [Fact]
    public void ClearTests()
    {
        LogRecordSharedPool.Resize(LogRecordSharedPool.DefaultMaxPoolSize);

        var pool = LogRecordSharedPool.Current;

        var logRecord1 = pool.Rent();
        logRecord1.AttributeStorage = new List<KeyValuePair<string, object?>>(16)
        {
            new KeyValuePair<string, object?>("key1", "value1"),
            new KeyValuePair<string, object?>("key2", "value2"),
        };
        logRecord1.ScopeStorage = new List<object?>(8) { null, null };

        pool.Return(logRecord1);

        Assert.Empty(logRecord1.AttributeStorage);
        Assert.Equal(16, logRecord1.AttributeStorage.Capacity);
        Assert.Empty(logRecord1.ScopeStorage);
        Assert.Equal(8, logRecord1.ScopeStorage.Capacity);

        logRecord1 = pool.Rent();

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

        pool.Return(logRecord1);

        Assert.Null(logRecord1.AttributeStorage);
        Assert.Null(logRecord1.ScopeStorage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportTest(bool warmup)
    {
        LogRecordSharedPool.Resize(LogRecordSharedPool.DefaultMaxPoolSize);

        var pool = LogRecordSharedPool.Current;

        if (warmup)
        {
            for (int i = 0; i < LogRecordSharedPool.DefaultMaxPoolSize; i++)
            {
                pool.Return(new LogRecord { PoolReferenceCount = 1 });
            }
        }

        using BatchLogRecordExportProcessor processor = new(new NoopExporter());

        List<Task> tasks = new();

        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                Random random = new Random();

                await Task.Delay(random.Next(100, 150));

                for (int i = 0; i < 1000; i++)
                {
                    var logRecord = pool.Rent();

                    processor.OnEnd(logRecord);

                    // This should no-op mostly.
                    pool.Return(logRecord);

                    await Task.Delay(random.Next(0, 20));
                }
            }));
        }

        await Task.WhenAll(tasks);

        processor.ForceFlush();

        if (warmup)
        {
            Assert.Equal(LogRecordSharedPool.DefaultMaxPoolSize, pool.Count);
        }

        Assert.True(pool.Count <= LogRecordSharedPool.DefaultMaxPoolSize);
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

        LogRecordSharedPool.Resize(LogRecordSharedPool.DefaultMaxPoolSize);

        var pool = LogRecordSharedPool.Current;

        List<Task> tasks = new();

        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(2000);

                for (int i = 0; i < 100_000; i++)
                {
                    var logRecord = pool.Rent();

                    pool.Return(logRecord);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.True(pool.Count <= LogRecordSharedPool.DefaultMaxPoolSize);
    }

    private sealed class NoopExporter : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            return ExportResult.Success;
        }
    }
}
