// <copyright file="LogRecordPool.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Manages a pool of <see cref="LogRecord"/> instances.
    /// </summary>
    public sealed class LogRecordPool
    {
        private const int DefaultMaxPoolSize = 1024;

        private static LogRecordPool current = new(DefaultMaxPoolSize);

        [ThreadStatic]
        private static LogRecord? threadStaticLogRecord;

        private readonly int sharedPoolSize;
        private readonly LogRecord?[] sharedPool;
        private int sharedPoolCurrentIndex = -1;

        private LogRecordPool(int size)
        {
            this.sharedPoolSize = size;
            this.sharedPool = new LogRecord?[size];
        }

        /// <summary>
        /// Resize the pool.
        /// </summary>
        /// <param name="size">The maximum number of <see cref="LogRecord"/>s to store in the pool.</param>
        public static void Resize(int size)
        {
            Guard.ThrowIfOutOfRange(size, min: 1);

            current = new LogRecordPool(size);
        }

        internal static LogRecord Rent() => current.RentCore();

        internal static void Return(LogRecord logRecord) => current.ReturnCore(logRecord);

        internal static void TrackReference(LogRecord logRecord)
            => Interlocked.Increment(ref logRecord.PoolReferences);

        private LogRecord RentCore()
        {
            LogRecord? logRecord = threadStaticLogRecord;

            if (logRecord != null)
            {
                threadStaticLogRecord = null;

                logRecord.PoolReferences = 1;

                return logRecord;
            }

            SpinWait wait = default;
            while (true)
            {
                int sharedPoolIndex = this.sharedPoolCurrentIndex;
                if (sharedPoolIndex < 0)
                {
                    break;
                }

                if (Interlocked.CompareExchange(ref this.sharedPoolCurrentIndex, sharedPoolIndex - 1, sharedPoolIndex) == sharedPoolIndex)
                {
                    while (true)
                    {
                        logRecord = this.sharedPool[sharedPoolIndex];
                        if (logRecord != null)
                        {
                            break;
                        }

                        // If logRecord was null it means we raced with the return call, retry.
                        wait.SpinOnce();
                    }

                    this.sharedPool[sharedPoolIndex] = null;

                    logRecord.PoolReferences = 1;

                    return logRecord;
                }

                wait.SpinOnce();
            }

            return new LogRecord()
            {
                PoolReferences = 1,
            };
        }

        private void ReturnCore(LogRecord logRecord)
        {
            int poolReferences = Interlocked.Decrement(ref logRecord.PoolReferences);
            if (poolReferences > 0)
            {
                return;
            }

            this.Clear(logRecord);

            if (threadStaticLogRecord == null)
            {
                threadStaticLogRecord = logRecord;
            }
            else
            {
                SpinWait wait = default;
                while (true)
                {
                    int sharedPoolIndex = this.sharedPoolCurrentIndex;
                    if (sharedPoolIndex >= this.sharedPoolSize)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref this.sharedPoolCurrentIndex, sharedPoolIndex + 1, sharedPoolIndex) == sharedPoolIndex)
                    {
                        this.sharedPool[sharedPoolIndex + 1] = logRecord;
                        break;
                    }

                    wait.SpinOnce();
                }
            }
        }

        private void Clear(LogRecord logRecord)
        {
            var attributeStorage = logRecord.AttributeStorage;
            if (attributeStorage != null)
            {
                if (attributeStorage.Count > 64)
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
                if (bufferedScopes.Count > 16)
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
    }
}
