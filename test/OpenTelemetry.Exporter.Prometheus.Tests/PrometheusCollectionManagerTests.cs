// <copyright file="PrometheusCollectionManagerTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
#if NET461
using System.Linq;
#endif
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public sealed class PrometheusCollectionManagerTests
    {
        [Fact]
        public async Task EnterExitCollectTest()
        {
            using var meter = new Meter(Utils.GetCurrentMethodName());

            using (var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddPrometheusExporter()
                .Build())
            {
                if (!provider.TryFindExporter(out PrometheusExporter exporter))
                {
                    throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
                }

                int runningCollectCount = 0;
                var collectFunc = exporter.Collect;
                exporter.Collect = (timeout) =>
                {
                    bool result = collectFunc(timeout);
                    runningCollectCount++;
                    Thread.Sleep(5000);
                    return result;
                };

                var counter = meter.CreateCounter<int>("counter_int", description: "Prometheus help text goes here \n escaping.");
                counter.Add(100);

                Task<byte[]>[] collectTasks = new Task<byte[]>[10];
                for (int i = 0; i < collectTasks.Length; i++)
                {
                    collectTasks[i] = Task.Run(async () =>
                    {
                        ArraySegment<byte> data = await exporter.CollectionManager.EnterCollect().ConfigureAwait(false);
                        try
                        {
                            return data.ToArray();
                        }
                        finally
                        {
                            exporter.CollectionManager.ExitCollect();
                        }
                    });
                }

                await Task.WhenAll(collectTasks).ConfigureAwait(false);

                Assert.Equal(1, runningCollectCount);

                byte[] firstBatch = collectTasks[0].Result;
                for (int i = 1; i < collectTasks.Length; i++)
                {
                    Assert.Equal(firstBatch, collectTasks[i].Result);
                }

                counter.Add(100);

                // This should use the cache and ignore the second counter update.
                var task = exporter.CollectionManager.EnterCollect();
                Assert.True(task.IsCompleted);
                ArraySegment<byte> data = await task.ConfigureAwait(false);
                try
                {
                    Assert.Equal(1, runningCollectCount);
                    Assert.Equal(firstBatch, data);
                }
                finally
                {
                    exporter.CollectionManager.ExitCollect();
                }

                Thread.Sleep(exporter.Options.ScrapeResponseCacheDurationMilliseconds);

                counter.Add(100);

                for (int i = 0; i < collectTasks.Length; i++)
                {
                    collectTasks[i] = Task.Run(async () =>
                    {
                        ArraySegment<byte> data = await exporter.CollectionManager.EnterCollect().ConfigureAwait(false);
                        try
                        {
                            return data.ToArray();
                        }
                        finally
                        {
                            exporter.CollectionManager.ExitCollect();
                        }
                    });
                }

                await Task.WhenAll(collectTasks).ConfigureAwait(false);

                Assert.Equal(2, runningCollectCount);
                Assert.NotEqual(firstBatch, collectTasks[0].Result);

                firstBatch = collectTasks[0].Result;
                for (int i = 1; i < collectTasks.Length; i++)
                {
                    Assert.Equal(firstBatch, collectTasks[i].Result);
                }
            }
        }
    }
}
