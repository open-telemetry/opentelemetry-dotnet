// <copyright file="PrometheusCollectionManager.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#if NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks.Sources;
#endif

namespace OpenTelemetry.Exporter.Prometheus
{
    internal class PrometheusCollectionManager
    {
        private readonly PrometheusExporter exporter;
        private byte[] buffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
        private int globalLockState;
        private ArraySegment<byte> previousDataView;
        private DateTime? previousDataViewExpirationAtUtc;
        private int readerCount;
#if NETCOREAPP3_1_OR_GREATER
        private bool collectionRunning;
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields
        private readonly ManualResetValueTaskSource<ArraySegment<byte>> collectionTcs = new ManualResetValueTaskSource<ArraySegment<byte>>()
        {
            RunContinuationsAsynchronously = false,
        };
#pragma warning restore SA1214 // Readonly fields should appear before non-readonly fields
#else
        private TaskCompletionSource<ArraySegment<byte>> collectionTcs;
#endif

        public PrometheusCollectionManager(PrometheusExporter exporter)
        {
            this.exporter = exporter;
        }

#if NETCOREAPP3_1_OR_GREATER
        public ValueTask<ArraySegment<byte>> EnterCollect()
#else
        public Task<ArraySegment<byte>> EnterCollect()
#endif
        {
            this.EnterGlobalLock();

            // If we are within 10 seconds of the last successful collect, return the previous view.
            if (this.previousDataViewExpirationAtUtc.HasValue && this.previousDataViewExpirationAtUtc >= DateTime.UtcNow)
            {
                Interlocked.Increment(ref this.readerCount);
                this.ExitGlobalLock();
#if NETCOREAPP3_1_OR_GREATER
                return new ValueTask<ArraySegment<byte>>(this.previousDataView);
#else
                return Task.FromResult(this.previousDataView);
#endif
            }

            // If a collection is already running, return a task to wait on the result.
#if NETCOREAPP3_1_OR_GREATER
            if (this.collectionRunning)
#else
            if (this.collectionTcs != null)
#endif
            {
                Interlocked.Increment(ref this.readerCount);
                this.ExitGlobalLock();
#if NETCOREAPP3_1_OR_GREATER
                return new ValueTask<ArraySegment<byte>>(this.collectionTcs, 0);
#else
                return this.collectionTcs.Task;
#endif
            }

            // Wait for all readers to complete.
            SpinWait readWait = default;
            while (true)
            {
                if (Interlocked.CompareExchange(ref this.readerCount, 0, this.readerCount) != 0)
                {
                    readWait.SpinOnce();
                    continue;
                }

                break;
            }

            // Start a collection on the current thread.
            this.previousDataViewExpirationAtUtc = null;
#if NETCOREAPP3_1_OR_GREATER
            this.collectionRunning = true;
#else
            this.collectionTcs = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();

            int count = this.ExecuteCollect();
            this.previousDataView = new ArraySegment<byte>(this.buffer, 0, count);
            if (count > 0)
            {
                this.previousDataViewExpirationAtUtc = DateTime.UtcNow.AddSeconds(10);
            }

            this.collectionTcs.SetResult(this.previousDataView);

            this.EnterGlobalLock();

#if NETCOREAPP3_1_OR_GREATER
            this.collectionTcs.Reset();
            this.collectionRunning = false;
#else
            this.collectionTcs = null;
#endif

            this.ExitGlobalLock();

#if NETCOREAPP3_1_OR_GREATER
            return new ValueTask<ArraySegment<byte>>(this.previousDataView);
#else
            return Task.FromResult(this.previousDataView);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitCollect()
        {
            Interlocked.Decrement(ref this.readerCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnterGlobalLock()
        {
            SpinWait lockWait = default;
            while (true)
            {
                if (Interlocked.CompareExchange(ref this.globalLockState, 1, this.globalLockState) != 0)
                {
                    lockWait.SpinOnce();
                    continue;
                }

                break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitGlobalLock()
        {
            this.globalLockState = 0;
        }

        private int ExecuteCollect()
        {
            int count = 0;

            this.exporter.OnExport = (metrics) =>
            {
                try
                {
                    foreach (var metric in metrics)
                    {
                        while (true)
                        {
                            try
                            {
                                count = PrometheusSerializer.WriteMetric(this.buffer, count, metric);
                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                int bufferSize = this.buffer.Length * 2;

                                // there are two cases we might run into the following condition:
                                // 1. we have many metrics to be exported - in this case we probably want
                                //    to put some upper limit and allow the user to configure it.
                                // 2. we got an IndexOutOfRangeException which was triggered by some other
                                //    code instead of the buffer[cursor++] - in this case we should give up
                                //    at certain point rather than allocating like crazy.
                                if (bufferSize > 100 * 1024 * 1024)
                                {
                                    throw;
                                }

                                var newBuffer = new byte[bufferSize];
                                this.buffer.CopyTo(newBuffer, 0);
                                this.buffer = newBuffer;
                            }
                        }
                    }

                    return ExportResult.Success;
                }
                catch (Exception)
                {
                    return ExportResult.Failure;
                }
            };

            bool result = this.exporter.Collect(Timeout.Infinite);
            this.exporter.OnExport = null;

            return result ? count : 0;
        }

#if NETCOREAPP3_1_OR_GREATER
        private sealed class ManualResetValueTaskSource<T> : IValueTaskSource<T>
        {
            private ManualResetValueTaskSourceCore<T> core; // mutable struct; do not make this readonly

            public bool RunContinuationsAsynchronously
            {
                get => this.core.RunContinuationsAsynchronously;
                set => this.core.RunContinuationsAsynchronously = value;
            }

            public void Reset() => this.core.Reset();

            public void SetResult(T result) => this.core.SetResult(result);

            public T GetResult(short token) => this.core.GetResult(token);

            public ValueTaskSourceStatus GetStatus(short token) => this.core.GetStatus(token);

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => this.core.OnCompleted(continuation, state, token, flags);
        }
#endif
    }
}
