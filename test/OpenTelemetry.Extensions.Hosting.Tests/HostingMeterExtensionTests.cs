// <copyright file="HostingMeterExtensionTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests
{
    public class HostingMeterExtensionTests
    {
        [Fact]
        public async Task AddOpenTelemetryMeterProviderInstrumentationCreationAndDisposal()
        {
            var testInstrumentation = new TestInstrumentation();
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryMetrics(builder =>
                {
                    builder.AddInstrumentation(() =>
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
        public void AddOpenTelemetryMeterProvider_HostBuilt_OpenTelemetrySdk_RegisteredAsSingleton()
        {
            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryMetrics();
            });

            var host = builder.Build();

            var meterProvider1 = host.Services.GetRequiredService<MeterProvider>();
            var meterProvider2 = host.Services.GetRequiredService<MeterProvider>();

            Assert.Same(meterProvider1, meterProvider2);
        }

        [Fact]
        public void AddOpenTelemetryMeterProvider_ServiceProviderArgument_ServicesRegistered()
        {
            var testInstrumentation = new TestInstrumentation();

            var services = new ServiceCollection();
            services.AddSingleton(testInstrumentation);
            services.AddOpenTelemetryMetrics(builder =>
            {
                builder.ConfigureBuilder(
                    (sp, b) => b.AddInstrumentation(() => sp.GetRequiredService<TestInstrumentation>()));
            });

            var serviceProvider = services.BuildServiceProvider();

            var meterFactory = serviceProvider.GetRequiredService<MeterProvider>();
            Assert.NotNull(meterFactory);

            Assert.False(testInstrumentation.Disposed);

            serviceProvider.Dispose();

            Assert.True(testInstrumentation.Disposed);
        }

        [Fact]
        public void AddOpenTelemetryMeterProvider_BadArgs_NullServiceCollection()
        {
            ServiceCollection services = null;
            Assert.Throws<ArgumentNullException>(() => services.AddOpenTelemetryMetrics(null));
            Assert.Throws<ArgumentNullException>(() =>
                services.AddOpenTelemetryMetrics(builder =>
                {
                    builder.ConfigureBuilder(
                        (sp, b) => b.AddInstrumentation(() => sp.GetRequiredService<TestInstrumentation>()));
                }));
        }

        [Fact]
        public void AddOpenTelemetryMeterProvider_GetServicesExtension()
        {
            var services = new ServiceCollection();
            services.AddOpenTelemetryMetrics(builder => AddMyFeature(builder));

            using var serviceProvider = services.BuildServiceProvider();

            var meterProvider = (MeterProviderSdk)serviceProvider.GetRequiredService<MeterProvider>();

            Assert.True(meterProvider.Reader is TestReader);
        }

        [Fact]
        public void AddOpenTelemetryMeterProvider_NestedConfigureCallbacks()
        {
            int configureCalls = 0;
            var services = new ServiceCollection();
            services.AddOpenTelemetryMetrics(builder => builder
                .ConfigureBuilder((sp1, builder1) =>
                {
                    configureCalls++;
                    builder1.ConfigureBuilder((sp2, builder2) =>
                    {
                        configureCalls++;
                    });
                }));

            using var serviceProvider = services.BuildServiceProvider();

            var meterFactory = serviceProvider.GetRequiredService<MeterProvider>();

            Assert.Equal(2, configureCalls);
        }

        [Fact]
        public void AddOpenTelemetryMeterProvider_ConfigureCallbacksUsingExtensions()
        {
            var services = new ServiceCollection();

            services.AddSingleton<TestInstrumentation>();
            services.AddSingleton<TestReader>();

            services.AddOpenTelemetryMetrics(builder => builder
                .ConfigureBuilder((sp1, builder1) =>
                {
                    builder1
                        .AddInstrumentation<TestInstrumentation>()
                        .AddReader<TestReader>();
                }));

            using var serviceProvider = services.BuildServiceProvider();

            var meterProvider = (MeterProviderSdk)serviceProvider.GetRequiredService<MeterProvider>();

            Assert.True(meterProvider.Instrumentations.FirstOrDefault() is TestInstrumentation);
            Assert.True(meterProvider.Reader is TestReader);
        }

        [Fact]
        public void AddOpenTelemetryMetrics_MultipleCallsConfigureSingleProvider()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetryMetrics(builder => builder.AddMeter("TestSourceBuilder1"));
            services.AddOpenTelemetryMetrics();
            services.AddOpenTelemetryMetrics(builder => builder.AddMeter("TestSourceBuilder2"));

            using var serviceProvider = services.BuildServiceProvider();

            var providers = serviceProvider.GetServices<MeterProvider>();

            Assert.Single(providers);
        }

        [Fact]
        public async Task AddOpenTelemetryMetrics_HostConfigurationHonoredTest()
        {
            bool configureBuilderCalled = false;

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["TEST_KEY"] = "TEST_KEY_VALUE",
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddOpenTelemetryMetrics(builder =>
                    {
                        builder.ConfigureBuilder((sp, builder) =>
                        {
                            configureBuilderCalled = true;

                            var configuration = sp.GetRequiredService<IConfiguration>();

                            var testKeyValue = configuration.GetValue<string>("TEST_KEY", null);

                            Assert.Equal("TEST_KEY_VALUE", testKeyValue);
                        });
                    });
                });

            var host = builder.Build();

            Assert.False(configureBuilderCalled);

            await host.StartAsync();

            Assert.True(configureBuilderCalled);

            await host.StopAsync();

            host.Dispose();
        }

        private static MeterProviderBuilder AddMyFeature(MeterProviderBuilder meterProviderBuilder)
        {
            meterProviderBuilder.ConfigureServices(services => services.AddSingleton<TestReader>());

            return meterProviderBuilder.AddReader<TestReader>();
        }

        internal class TestInstrumentation : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }

        internal class TestReader : MetricReader
        {
        }
    }
}
