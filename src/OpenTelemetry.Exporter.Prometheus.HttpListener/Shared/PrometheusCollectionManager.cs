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
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.HttpListener.Shared
{
    internal sealed class PrometheusCollectionManager
    {
        private readonly PrometheusExporter exporter;
        private readonly int scrapeResponseCacheDurationInMilliseconds;
        private readonly Func<Batch<Metric>, ExportResult> onCollectRef;
        private byte[] buffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
        private int globalLockState;
        private ArraySegment<byte> previousDataView;
        private DateTime? previousDataViewGeneratedAtUtc;
        private int readerCount;
        private bool collectionRunning;
        private TaskCompletionSource<CollectionResponse> collectionTcs;

        public PrometheusCollectionManager(PrometheusExporter exporter)
        {
            this.exporter = exporter;
            this.scrapeResponseCacheDurationInMilliseconds = this.exporter.Options.ScrapeResponseCacheDurationMilliseconds;
            this.onCollectRef = this.OnCollect;
        }

#if NETCOREAPP3_1_OR_GREATER
        public ValueTask<CollectionResponse> EnterCollect()
#else
        public Task<CollectionResponse> EnterCollect()
#endif
        {
            this.EnterGlobalLock();

            // If we are within {ScrapeResponseCacheDurationMilliseconds} of the
            // last successful collect, return the previous view.
            if (this.previousDataViewGeneratedAtUtc.HasValue
                && this.scrapeResponseCacheDurationInMilliseconds > 0
                && this.previousDataViewGeneratedAtUtc.Value.AddMilliseconds(this.scrapeResponseCacheDurationInMilliseconds) >= DateTime.UtcNow)
            {
                Interlocked.Increment(ref this.readerCount);
                this.ExitGlobalLock();
#if NETCOREAPP3_1_OR_GREATER
                return new ValueTask<CollectionResponse>(new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, fromCache: true));
#else
                return Task.FromResult(new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, fromCache: true));
#endif
            }

            // If a collection is already running, return a task to wait on the result.
            if (this.collectionRunning)
            {
                if (this.collectionTcs == null)
                {
                    this.collectionTcs = new TaskCompletionSource<CollectionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                Interlocked.Increment(ref this.readerCount);
                this.ExitGlobalLock();
#if NETCOREAPP3_1_OR_GREATER
                return new ValueTask<CollectionResponse>(this.collectionTcs.Task);
#else
                return this.collectionTcs.Task;
#endif
            }

            this.WaitForReadersToComplete();

            // Start a collection on the current thread.
            this.collectionRunning = true;
            this.previousDataViewGeneratedAtUtc = null;
            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();

            CollectionResponse response;
            var result = this.ExecuteCollect();
            if (result)
            {
                this.previousDataViewGeneratedAtUtc = DateTime.UtcNow;
                response = new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, fromCache: false);
            }
            else
            {
                response = default;
            }

            this.EnterGlobalLock();

            this.collectionRunning = false;

            if (this.collectionTcs != null)
            {
                this.collectionTcs.SetResult(response);
                this.collectionTcs = null;
            }

            this.ExitGlobalLock();

#if NETCOREAPP3_1_OR_GREATER
            return new ValueTask<CollectionResponse>(response);
#else
            return Task.FromResult(response);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WaitForReadersToComplete()
        {
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExecuteCollect()
        {
            this.exporter.OnExport = this.onCollectRef;
            var result = this.exporter.Collect(Timeout.Infinite);
            this.exporter.OnExport = null;
            return result;
        }

        private ExportResult OnCollect(Batch<Metric> metrics)
        {
            var cursor = 0;

            try
            {
                foreach (var metric in metrics)
                {
                    while (true)
                    {
                        try
                        {
                            cursor = PrometheusSerializer.WriteMetric(this.buffer, cursor, metric);
                            break;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            var bufferSize = this.buffer.Length * 2;

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

                this.previousDataView = new ArraySegment<byte>(this.buffer, 0, Math.Max(cursor - 1, 0));
                return ExportResult.Success;
            }
            catch (Exception)
            {
                this.previousDataView = new ArraySegment<byte>(Array.Empty<byte>(), 0, 0);
                return ExportResult.Failure;
            }
        }

        public readonly struct CollectionResponse
        {
            public CollectionResponse(ArraySegment<byte> view, DateTime generatedAtUtc, bool fromCache)
            {
                this.View = view;
                this.GeneratedAtUtc = generatedAtUtc;
                this.FromCache = fromCache;
            }

            public ArraySegment<byte> View { get; }

            public DateTime GeneratedAtUtc { get; }

            public bool FromCache { get; }
        }
    }
}
