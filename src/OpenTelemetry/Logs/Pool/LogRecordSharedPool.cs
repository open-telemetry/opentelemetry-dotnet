// <copyright file="LogRecordSharedPool.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenTelemetry.Logs
{
    internal sealed class LogRecordSharedPool : ILogRecordPool
    {
        internal const int DefaultMaxPoolSize = 2048;
        internal const int DefaultMaxNumberOfAttributes = 64;
        internal const int DefaultMaxNumberOfScopes = 16;

        internal static LogRecordSharedPool Current = new(DefaultMaxPoolSize);

        internal readonly int Capacity;
        private readonly LogRecord?[] pool;
        private long rentIndex;
        private long returnIndex;

        internal LogRecordSharedPool(int capacity)
        {
            this.Capacity = capacity;
            this.pool = new LogRecord?[capacity];
        }

        internal int Count => (int)(Volatile.Read(ref this.returnIndex) - Volatile.Read(ref this.rentIndex));

        public LogRecord Rent()
        {
            while (true)
            {
                var rentSnapshot = Volatile.Read(ref this.rentIndex);
                var returnSnapshot = Volatile.Read(ref this.returnIndex);

                if (rentSnapshot >= returnSnapshot)
                {
                    break; // buffer is empty
                }

                if (Interlocked.CompareExchange(ref this.rentIndex, rentSnapshot + 1, rentSnapshot) == rentSnapshot)
                {
                    var logRecord = Interlocked.Exchange(ref this.pool[rentSnapshot % this.Capacity], null);
                    if (logRecord == null && !this.TryRentCoreRare(rentSnapshot, out logRecord))
                    {
                        continue;
                    }

                    logRecord.PoolReferenceCount = 1;
                    return logRecord;
                }
            }

            return new LogRecord()
            {
                PoolReferenceCount = 1,
            };
        }

        public void Return(LogRecord logRecord)
        {
            if (Interlocked.Decrement(ref logRecord.PoolReferenceCount) != 0)
            {
                return;
            }

            Clear(logRecord);

            while (true)
            {
                var rentSnapshot = Volatile.Read(ref this.rentIndex);
                var returnSnapshot = Volatile.Read(ref this.returnIndex);

                if (returnSnapshot - rentSnapshot >= this.Capacity)
                {
                    return; // buffer is full
                }

                if (Interlocked.CompareExchange(ref this.returnIndex, returnSnapshot + 1, returnSnapshot) == returnSnapshot)
                {
                    // If many threads are hammering rent/return it is possible
                    // for two threads to write to the same index. In that case
                    // only one of the logRecords will make it back into the
                    // pool. Anything lost in the race will collected by the GC
                    // and the pool will issue new instances as needed
                    this.pool[returnSnapshot % this.Capacity] = logRecord;
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackReference(LogRecord logRecord)
        {
            Interlocked.Increment(ref logRecord.PoolReferenceCount);
        }

        internal static void Clear(LogRecord logRecord)
        {
            var attributeStorage = logRecord.AttributeStorage;
            if (attributeStorage != null)
            {
                if (attributeStorage.Count > DefaultMaxNumberOfAttributes)
                {
                    // Don't allow the pool to grow unconstained.
                    logRecord.AttributeStorage = null;
                }
                else
                {
                    /* List<T>.Clear sets the size to 0 but it maintains the
                    underlying array. */
                    attributeStorage.Clear();
                }
            }

            var bufferedScopes = logRecord.BufferedScopes;
            if (bufferedScopes != null)
            {
                if (bufferedScopes.Count > DefaultMaxNumberOfScopes)
                {
                    // Don't allow the pool to grow unconstained.
                    logRecord.BufferedScopes = null;
                }
                else
                {
                    /* List<T>.Clear sets the size to 0 but it maintains the
                    underlying array. */
                    bufferedScopes.Clear();
                }
            }
        }

        private bool TryRentCoreRare(long rentSnapshot, [NotNullWhen(true)] out LogRecord? logRecord)
        {
            SpinWait wait = default;
            while (true)
            {
                if (wait.NextSpinWillYield)
                {
                    // Super rare case. If many threads are hammering
                    // rent/return it is possible a read was issued an index and
                    // then yielded while other threads caused the pointers to
                    // wrap around. When the yielded thread wakes up its read
                    // index could have been stolen by another thread. To
                    // prevent deadlock, bail out of read after spinning. This
                    // will cause either a successful rent from another index,
                    // or a new record to be created
                    logRecord = null;
                    return false;
                }

                wait.SpinOnce();

                logRecord = Interlocked.Exchange(ref this.pool[rentSnapshot % this.Capacity], null);
                if (logRecord != null)
                {
                    // Rare case where the write was still working when the read came in
                    return true;
                }
            }
        }
    }
}
