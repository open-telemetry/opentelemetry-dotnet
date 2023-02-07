// <copyright file="OpenTelemetryServiceCollectionExtensionsTests.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests;

public class OpenTelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOpenTelemetry_HostedService_Registered()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry();

        using var serviceProvider = services.BuildServiceProvider();

        var hostedServices = serviceProvider.GetServices<IHostedService>();

        Assert.NotEmpty(hostedServices);
    }

    [Fact]
    public async Task AddOpenTelemetry_HostedService_WithoutProvidersDoesNotThrow()
    {
        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddOpenTelemetry();
        });

        var host = builder.Build();

        await host.StartAsync().ConfigureAwait(false);

        await host.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task AddOpenTelemetry_HostedService_StartWithExceptionsThrows()
    {
        bool expectedInnerExceptionThrown = false;

        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
                    {
                        deferredTracerProviderBuilder.Configure((sp, sdkBuilder) =>
                        {
                            try
                            {
                                // Note: This throws because services cannot be
                                // registered after IServiceProvider has been
                                // created.
                                sdkBuilder.SetSampler<MySampler>();
                            }
                            catch (NotSupportedException)
                            {
                                expectedInnerExceptionThrown = true;
                                throw;
                            }
                        });
                    }
                });
        });

        var host = builder.Build();

        await Assert.ThrowsAsync<NotSupportedException>(() => host.StartAsync()).ConfigureAwait(false);

        await host.StopAsync().ConfigureAwait(false);

        Assert.True(expectedInnerExceptionThrown);
    }

    [Fact]
    public async Task AddOpenTelemetry_WithMetrics_CreationAndDisposal()
    {
        var callbackRun = false;

        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddOpenTelemetry()
                .WithMetrics(builder => builder
                    .AddInstrumentation(() =>
                    {
                        callbackRun = true;
                        return new object();
                    }));
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
    public async Task AddOpenTelemetry_WithMetrics_HostConfigurationHonoredTest()
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
                    .WithMetrics(builder =>
                    {
                        if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
                        {
                            deferredMeterProviderBuilder.Configure((sp, builder) =>
                            {
                                configureBuilderCalled = true;

                                var configuration = sp.GetRequiredService<IConfiguration>();

                                var testKeyValue = configuration.GetValue<string?>("TEST_KEY", null);

                                Assert.Equal("TEST_KEY_VALUE", testKeyValue);
                            });
                        }
                    });
            });

        var host = builder.Build();

        Assert.False(configureBuilderCalled);

        await host.StartAsync().ConfigureAwait(false);

        Assert.True(configureBuilderCalled);

        await host.StopAsync().ConfigureAwait(false);

        host.Dispose();
    }

    [Fact]
    public async Task AddOpenTelemetry_WithTracing_CreationAndDisposal()
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
                    }));
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
    public async Task AddOpenTelemetry_WithTracing_HostConfigurationHonoredTest()
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
                    .WithTracing(builder =>
                    {
                        if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
                        {
                            deferredTracerProviderBuilder.Configure((sp, builder) =>
                            {
                                configureBuilderCalled = true;

                                var configuration = sp.GetRequiredService<IConfiguration>();

                                var testKeyValue = configuration.GetValue<string?>("TEST_KEY", null);

                                Assert.Equal("TEST_KEY_VALUE", testKeyValue);
                            });
                        }
                    });
            });

        var host = builder.Build();

        Assert.False(configureBuilderCalled);

        await host.StartAsync().ConfigureAwait(false);

        Assert.True(configureBuilderCalled);

        await host.StopAsync().ConfigureAwait(false);

        host.Dispose();
    }

    private sealed class MySampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
