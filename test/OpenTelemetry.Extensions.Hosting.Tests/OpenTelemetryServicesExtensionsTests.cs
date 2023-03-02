// <copyright file="OpenTelemetryServicesExtensionsTests.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class OpenTelemetryServicesExtensionsTests
{
    [Fact]
    public async Task AddOpenTelemetry_StartWithoutProvidersDoesNotThrow()
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
    public async Task AddOpenTelemetry_StartWithExceptionsThrows()
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
    public void AddOpenTelemetry_WithTracing_SingleProviderForServiceCollectionTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithTracing(builder => { });

        services.AddOpenTelemetry().WithTracing(builder => { });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider);

        var tracerProviders = serviceProvider.GetServices<TracerProvider>();

        Assert.Single(tracerProviders);
    }

    [Fact]
    public void AddOpenTelemetry_WithTracing_DisposalTest()
    {
        var services = new ServiceCollection();

        bool testRun = false;

        services.AddOpenTelemetry().WithTracing(builder =>
        {
            testRun = true;

            // Note: Build can't be called directly on builder tied to external services
            Assert.Throws<NotSupportedException>(() => builder.Build());
        });

        Assert.True(testRun);

        var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<TracerProvider>() as TracerProviderSdk;

        Assert.NotNull(provider);
        Assert.Null(provider.OwnedServiceProvider);

        Assert.NotNull(serviceProvider);
        Assert.NotNull(provider);

        Assert.False(provider.Disposed);

        serviceProvider.Dispose();

        Assert.True(provider.Disposed);
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

                                var testKeyValue = configuration.GetValue<string>("TEST_KEY", null);

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
    public void AddOpenTelemetry_WithTracing_NestedResolutionUsingConfigureTest()
    {
        bool innerTestExecuted = false;

        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithTracing(builder =>
        {
            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    innerTestExecuted = true;
                    Assert.Throws<NotSupportedException>(() => sp.GetService<TracerProvider>());
                });
            }
        });

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedProvider = serviceProvider.GetRequiredService<TracerProvider>();
        Assert.True(innerTestExecuted);
    }

    [Fact]
    public void AddOpenTelemetry_WithMetrics_SingleProviderForServiceCollectionTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithMetrics(builder => { });

        services.AddOpenTelemetry().WithMetrics(builder => { });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider);

        var meterProviders = serviceProvider.GetServices<MeterProvider>();

        Assert.Single(meterProviders);
    }

    [Fact]
    public void AddOpenTelemetry_WithMetrics_DisposalTest()
    {
        var services = new ServiceCollection();

        bool testRun = false;

        services.AddOpenTelemetry().WithMetrics(builder =>
        {
            testRun = true;

            // Note: Build can't be called directly on builder tied to external services
            Assert.Throws<NotSupportedException>(() => builder.Build());
        });

        Assert.True(testRun);

        var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<MeterProvider>() as MeterProviderSdk;

        Assert.NotNull(provider);
        Assert.Null(provider.OwnedServiceProvider);

        Assert.NotNull(serviceProvider);
        Assert.NotNull(provider);

        Assert.False(provider.Disposed);

        serviceProvider.Dispose();

        Assert.True(provider.Disposed);
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

                                var testKeyValue = configuration.GetValue<string>("TEST_KEY", null);

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
    public void AddOpenTelemetry_WithMetrics_NestedResolutionUsingConfigureTest()
    {
        bool innerTestExecuted = false;

        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithMetrics(builder =>
        {
            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    innerTestExecuted = true;
                    Assert.Throws<NotSupportedException>(() => sp.GetService<MeterProvider>());
                });
            }
        });

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedProvider = serviceProvider.GetRequiredService<MeterProvider>();
        Assert.True(innerTestExecuted);
    }

    private sealed class MySampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(SamplingDecision.RecordAndSample);
    }
}
