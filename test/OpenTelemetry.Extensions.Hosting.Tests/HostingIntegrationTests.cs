// <copyright file="StackExchangeRedisCallsAdapterTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.Hosting
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
        public async Task AddOpenTelemetry_RegisterAdapter_AdapterCreatedAndDisposed()
        {
            var testAdapter = new TestAdapter();
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry(telemetry =>
                {
                    telemetry.AddAdapter(t =>
                    {
                        callbackRun = true;
                        return testAdapter;
                    });
                });
            });

            var host = builder.Build();

            Assert.False(callbackRun);
            Assert.False(testAdapter.Disposed);

            await host.StartAsync();

            Assert.True(callbackRun);
            Assert.False(testAdapter.Disposed);

            await host.StopAsync();

            Assert.True(callbackRun);
            Assert.False(testAdapter.Disposed);

            host.Dispose();

            Assert.True(callbackRun);
            Assert.True(testAdapter.Disposed);
        }

        [Fact]
        public void AddOpenTelemetry_HostBuilt_TracerFactoryRegisteredAsSingleton()
        {
            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry();
            });

            var host = builder.Build();

            var tracerFactoryBase1 = host.Services.GetRequiredService<TracerFactoryBase>();
            var tracerFactoryBase2 = host.Services.GetRequiredService<TracerFactoryBase>();

            Assert.Same(tracerFactoryBase1, tracerFactoryBase2);

            var tracerFactory1 = host.Services.GetRequiredService<TracerFactory>();
            var tracerFactory2 = host.Services.GetRequiredService<TracerFactory>();

            Assert.Same(tracerFactory1, tracerFactory2);
        }

        [Fact]
        public void AddOpenTelemetry_ServiceProviderArgument_ServicesRegistered()
        {
            var testAdapter = new TestAdapter();

            var services = new ServiceCollection();
            services.AddSingleton(testAdapter);
            services.AddOpenTelemetry((provider, builder) =>
            {
                builder.AddAdapter<TestAdapter>(tracer => provider.GetRequiredService<TestAdapter>());
            });

            var serviceProvider = services.BuildServiceProvider();

            var tracerFactory = serviceProvider.GetRequiredService<TracerFactory>();
            Assert.NotNull(tracerFactory);

            Assert.False(testAdapter.Disposed);

            serviceProvider.Dispose();

            Assert.True(testAdapter.Disposed);
        }

        internal class TestAdapter : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
