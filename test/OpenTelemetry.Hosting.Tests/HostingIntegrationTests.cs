// <copyright file="StackExchangeRedisCallsCollectorTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Hosting
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Xunit;

    public class HostingIntegrationTests
    {
        [Fact]
        public async Task RunHost_RegisterCollections_CollectorsCreatedAndDisposed()
        {
            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry(telemetry =>
                {
                    telemetry.AddCollector<TestCollector>();
                    telemetry.AddCollector<TestCollectorWithOptions>(new TestCollectorOptions());
                });
            });

            var host = builder.Build();

            await host.StartAsync();
            await host.StopAsync();

            var testCollector = host.Services.GetRequiredService<TestCollector>();
            Assert.True(testCollector.Disposed);

            var testCollectorWithOptions = host.Services.GetRequiredService<TestCollectorWithOptions>();
            Assert.True(testCollectorWithOptions.Disposed);
        }

        internal class TestCollector : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        internal class TestCollectorWithOptions : IDisposable
        {
            public bool Disposed { get; private set; }
            public TestCollectorOptions Options { get; }

            public TestCollectorWithOptions(TestCollectorOptions options)
            {
                Options = options;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        internal class TestCollectorOptions
        {

        }
    }
}
