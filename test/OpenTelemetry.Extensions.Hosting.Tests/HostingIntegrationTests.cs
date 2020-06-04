// <copyright file="HostingIntegrationTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting
{
    public class HostingIntegrationTests
    {
        [Fact]
        public async Task AddOpenTelemetry_RegisterInstrumentation_InstrumentationCreatedAndDisposed()
        {
            var testInstrumentation = new TestInstrumentation();
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry(telemetry =>
                {
                    telemetry.AddInstrumentation(t =>
                    {
                        callbackRun = true;
                        return testInstrumentation;
                    });
                });
            });

            var host = builder.Build();

            Assert.False(callbackRun);
            Assert.False(testInstrumentation.Disposed);

            await host.StartAsync();

            Assert.True(callbackRun);
            Assert.False(testInstrumentation.Disposed);

            await host.StopAsync();

            Assert.True(callbackRun);
            Assert.False(testInstrumentation.Disposed);

            host.Dispose();

            Assert.True(callbackRun);
            Assert.True(testInstrumentation.Disposed);
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
            var testInstrumentation = new TestInstrumentation();

            var services = new ServiceCollection();
            services.AddSingleton(testInstrumentation);
            services.AddOpenTelemetry((provider, builder) =>
            {
                builder.AddInstrumentation<TestInstrumentation>(tracer => provider.GetRequiredService<TestInstrumentation>());
            });

            var serviceProvider = services.BuildServiceProvider();

            var tracerFactory = serviceProvider.GetRequiredService<TracerFactory>();
            Assert.NotNull(tracerFactory);

            Assert.False(testInstrumentation.Disposed);

            serviceProvider.Dispose();

            Assert.True(testInstrumentation.Disposed);
        }

        internal class TestInstrumentation : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }
    }
}
