// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
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

        await host.StartAsync();

        await host.StopAsync();
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

        await Assert.ThrowsAsync<NotSupportedException>(() => host.StartAsync());

        await host.StopAsync();

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
                builder.AddInMemoryCollection(new Dictionary<string, string?>
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

        await host.StartAsync();

        Assert.True(configureBuilderCalled);

        await host.StopAsync();

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
                builder.AddInMemoryCollection(new Dictionary<string, string?>
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

        await host.StartAsync();

        Assert.True(configureBuilderCalled);

        await host.StopAsync();

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

    [Fact]
    public void AddOpenTelemetry_WithLogging_SingleProviderForServiceCollectionTest()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithLogging(builder => { });

        services.AddOpenTelemetry().WithLogging(builder => { });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider);

        var loggerProviders = serviceProvider.GetServices<LoggerProvider>();

        Assert.Single(loggerProviders);
    }

    [Fact]
    public void AddOpenTelemetry_WithLogging_DisposalTest()
    {
        var services = new ServiceCollection();

        bool testRun = false;

        services.AddOpenTelemetry().WithLogging(builder =>
        {
            testRun = true;

            // Note: Build can't be called directly on builder tied to external services
            Assert.Throws<NotSupportedException>(() => builder.Build());
        });

        Assert.True(testRun);

        var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(provider);
        Assert.Null(provider.OwnedServiceProvider);

        Assert.NotNull(serviceProvider);
        Assert.NotNull(provider);

        Assert.False(provider.Disposed);

        serviceProvider.Dispose();

        Assert.True(provider.Disposed);
    }

    [Fact]
    public void AddOpenTelemetry_WithLogging_HostConfigurationHonoredTest()
    {
        bool configureBuilderCalled = false;

        var builder = new HostBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TEST_KEY"] = "TEST_KEY_VALUE",
                });
            })
            .ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithLogging(builder =>
                    {
                        if (builder is IDeferredLoggerProviderBuilder deferredLoggerProviderBuilder)
                        {
                            deferredLoggerProviderBuilder.Configure((sp, builder) =>
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

        Assert.True(configureBuilderCalled);

        host.Dispose();
    }

    [Fact]
    public void AddOpenTelemetry_WithLogging_NestedResolutionUsingConfigureTest()
    {
        bool innerTestExecuted = false;

        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithLogging(builder =>
        {
            if (builder is IDeferredLoggerProviderBuilder deferredLoggerProviderBuilder)
            {
                deferredLoggerProviderBuilder.Configure((sp, builder) =>
                {
                    innerTestExecuted = true;
                    Assert.Throws<NotSupportedException>(() => sp.GetService<LoggerProvider>());
                });
            }
        });

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedProvider = serviceProvider.GetRequiredService<LoggerProvider>();
        Assert.True(innerTestExecuted);
    }

    [Fact]
    public async Task AddOpenTelemetry_HostedServiceOrder_DoesNotMatter()
    {
        var exportedItems = new List<Activity>();

        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddHostedService<TestHostedService>();
            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder.SetSampler(new AlwaysOnSampler());
                    builder.AddSource(nameof(TestHostedService));
                    builder.AddInMemoryExporter(exportedItems);
                });
        });

        var host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();
        host.Dispose();

        Assert.Single(exportedItems);
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class MySampler : Sampler
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(SamplingDecision.RecordAndSample);
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class TestHostedService : BackgroundService
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        private readonly ActivitySource activitySource = new(nameof(TestHostedService));

        public override void Dispose()
        {
            this.activitySource.Dispose();
            base.Dispose();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var activity = this.activitySource.StartActivity("test"))
            {
            }

            return Task.CompletedTask;
        }
    }
}
