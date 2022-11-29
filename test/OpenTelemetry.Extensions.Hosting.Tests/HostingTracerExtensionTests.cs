// <copyright file="HostingTracerExtensionTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests
{
    public class HostingTracerExtensionTests
    {
        [Fact]
        public async Task AddOpenTelemetryTracerProviderInstrumentationCreationAndDisposal()
        {
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryTracing(builder =>
                {
                    builder.AddInstrumentation(() =>
                    {
                        callbackRun = true;
                        return new object();
                    });
                });
            });

            var host = builder.Build();

            Assert.False(callbackRun);

            await host.StartAsync();

            Assert.True(callbackRun);

            await host.StopAsync();

            Assert.True(callbackRun);

            host.Dispose();

            Assert.True(callbackRun);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void AddOpenTelemetryTracerProvider_HostBuilt_OpenTelemetrySdk_RegisteredAsSingleton(bool callPreConfigure, bool callPostConfigure)
        {
            bool preConfigureCalled = false;
            bool postConfigureCalled = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                if (callPreConfigure)
                {
                    services.ConfigureOpenTelemetryTracing(builder => builder.ConfigureBuilder((sp, builder) => preConfigureCalled = true));
                }

                services.AddOpenTelemetryTracing();

                if (callPostConfigure)
                {
                    services.ConfigureOpenTelemetryTracing(builder => builder.ConfigureBuilder((sp, builder) => postConfigureCalled = true));
                }
            });

            var host = builder.Build();

            var tracerProvider1 = host.Services.GetRequiredService<TracerProvider>();
            var tracerProvider2 = host.Services.GetRequiredService<TracerProvider>();

            Assert.Same(tracerProvider1, tracerProvider2);

            Assert.Equal(callPreConfigure, preConfigureCalled);
            Assert.Equal(callPostConfigure, postConfigureCalled);
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_BadArgs_NullServiceCollection()
        {
            ServiceCollection services = null;
            Assert.Throws<ArgumentNullException>(() => services.AddOpenTelemetryTracing());

            services = new();
            Assert.Throws<ArgumentNullException>(() => services.AddOpenTelemetryTracing(null));
        }

        [Fact]
        public void AddOpenTelemetryTracing_MultipleCallsConfigureSingleProvider()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetryTracing(builder => builder.AddSource("TestSourceBuilder1"));
            services.AddOpenTelemetryTracing();
            services.AddOpenTelemetryTracing(builder => builder.AddSource("TestSourceBuilder2"));

            using var serviceProvider = services.BuildServiceProvider();

            var providers = serviceProvider.GetServices<TracerProvider>();

            Assert.Single(providers);
        }

        [Fact]
        public async Task AddOpenTelemetryTracing_HostConfigurationHonoredTest()
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
                    services.AddOpenTelemetryTracing(builder =>
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
    }
}
