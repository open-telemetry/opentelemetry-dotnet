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
        public async Task AddOpenTelemetry_StartWithHost_CreationAndDisposal()
        {
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddInstrumentation(() =>
                        {
                            callbackRun = true;
                            return new object();
                        }))
                    .StartWithHost();
            });

            var host = builder.Build();

            Assert.False(callbackRun);

            await host.StartAsync().ConfigureAwait(false);

            Assert.True(callbackRun);

            await host.StopAsync().ConfigureAwait(false);

            Assert.True(callbackRun);

            host.Dispose();

            Assert.True(callbackRun);
        }

        [Fact]
        public async Task AddOpenTelemetry_StartWithHost_HostConfigurationHonoredTest()
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
                    services.AddOpenTelemetry()
                        .WithTracing(builder => builder
                            .ConfigureBuilder((sp, builder) =>
                            {
                                configureBuilderCalled = true;

                                var configuration = sp.GetRequiredService<IConfiguration>();

                                var testKeyValue = configuration.GetValue<string>("TEST_KEY", null);

                                Assert.Equal("TEST_KEY_VALUE", testKeyValue);
                            }))
                        .StartWithHost();
                });

            var host = builder.Build();

            Assert.False(configureBuilderCalled);

            await host.StartAsync().ConfigureAwait(false);

            Assert.True(configureBuilderCalled);

            await host.StopAsync().ConfigureAwait(false);

            host.Dispose();
        }
    }
}
