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
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Configuration;
    using Xunit;

    public class HostingIntegrationTests
    {
        [Fact]
        public async Task AddOpenTelemetry_RegisterCollector_CollectorCreatedAndDisposed()
        {
            var testCollector = new TestCollector();
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry(telemetry =>
                {
                    telemetry.AddCollector(t =>
                    {
                        callbackRun = true;
                        return testCollector;
                    });
                });
            });

            var host = builder.Build();

            Assert.False(callbackRun);
            Assert.False(testCollector.Disposed);

            await host.StartAsync();

            Assert.True(callbackRun);
            Assert.False(testCollector.Disposed);

            await host.StopAsync();

            Assert.True(callbackRun);
            Assert.False(testCollector.Disposed);

            host.Dispose();

            Assert.True(callbackRun);
            Assert.True(testCollector.Disposed);
        }

        [Fact]
        public void AddOpenTelemetry_HostBuilt_TracerProviderRegisteredAsSingleton()
        {
            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry();
            });

            var host = builder.Build();

            var tracerProviderBase1 = host.Services.GetRequiredService<TracerProviderBase>();
            var tracerProviderBase2 = host.Services.GetRequiredService<TracerProviderBase>();

            Assert.Same(tracerProviderBase1, tracerProviderBase2);

            var tracerProvider1 = host.Services.GetRequiredService<TracerProvider>();
            var tracerProvider2 = host.Services.GetRequiredService<TracerProvider>();

            Assert.Same(tracerProvider1, tracerProvider2);
        }

        [Fact]
        public void AddOpenTelemetry_ServiceProviderArgument_ServicesRegistered()
        {
            var testCollector = new TestCollector();

            var services = new ServiceCollection();
            services.AddSingleton(testCollector);
            services.AddOpenTelemetry((provider, builder) =>
            {
                builder.AddCollector<TestCollector>(tracer => provider.GetRequiredService<TestCollector>());
            });

            var serviceProvider = services.BuildServiceProvider();

            var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();
            Assert.NotNull(tracerProvider);

            Assert.False(testCollector.Disposed);

            serviceProvider.Dispose();

            Assert.True(testCollector.Disposed);
        }

        internal class TestCollector : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
