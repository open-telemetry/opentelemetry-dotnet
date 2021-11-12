// <copyright file="PrometheusCollectManager.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Prometheus
{
    internal static class PrometheusCollectManager
    {
        private static byte[] buffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
        private static int lockState;
        private static ArraySegment<byte> previousData;
        private static DateTime? previousDataExpirationAtUtc;
        private static int readerCount;
        private static TaskCompletionSource<ArraySegment<byte>> collectTcs;

        public static ValueTask<ArraySegment<byte>> EnterCollect(PrometheusExporter exporter)
        {
            EnterGlobalLock();

            if (previousDataExpirationAtUtc.HasValue && previousDataExpirationAtUtc < DateTime.UtcNow)
            {
                Interlocked.Increment(ref readerCount);
                ExitGlobalLock();
                return new ValueTask<ArraySegment<byte>>(previousData);
            }

            if (collectTcs != null)
            {
                Interlocked.Increment(ref readerCount);
                ExitGlobalLock();
                return new ValueTask<ArraySegment<byte>>(collectTcs.Task);
            }

            SpinWait readWait = default;
            while (true)
            {
                if (Interlocked.CompareExchange(ref readerCount, 0, readerCount) != 0)
                {
                    readWait.SpinOnce();
                    continue;
                }

                break;
            }

            previousDataExpirationAtUtc = null;
            collectTcs = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Increment(ref readerCount);
            ExitGlobalLock();

            int count = ExecuteCollect(exporter);
            previousData = new ArraySegment<byte>(buffer, 0, count);
            if (count > 0)
            {
                previousDataExpirationAtUtc = DateTime.UtcNow.AddSeconds(10);
            }

            collectTcs.SetResult(previousData);

            EnterGlobalLock();

            collectTcs = null;

            ExitGlobalLock();

            return new ValueTask<ArraySegment<byte>>(previousData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitCollect()
        {
            Interlocked.Decrement(ref readerCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnterGlobalLock()
        {
            SpinWait lockWait = default;
            while (true)
            {
                if (Interlocked.CompareExchange(ref lockState, 1, lockState) != 0)
                {
                    lockWait.SpinOnce();
                    continue;
                }

                break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExitGlobalLock()
        {
            lockState = 0;
        }

        private static int ExecuteCollect(PrometheusExporter exporter)
        {
            int count = 0;

            exporter.OnExport = (metrics) =>
            {
                try
                {
                    foreach (var metric in metrics)
                    {
                        while (true)
                        {
                            try
                            {
                                count = PrometheusSerializer.WriteMetric(buffer, count, metric);
                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                int bufferSize = buffer.Length * 2;

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
                                buffer.CopyTo(newBuffer, 0);
                                buffer = newBuffer;
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

            bool result = exporter.Collect(Timeout.Infinite);
            exporter.OnExport = null;

            return result ? count : 0;
        }
    }
}
