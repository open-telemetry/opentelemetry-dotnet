// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#if NET11_0_OR_GREATER
using Microsoft.Extensions.Diagnostics.Tracing;
#endif
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Hosting.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public class OpenTelemetryServicesExtensionsTests
{
    [Fact]
    public async Task AddOpenTelemetry_StartWithoutProvidersDoesNotThrow()
    {
        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddOpenTelemetry();
        });

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddOpenTelemetry_StartWithExceptionsThrows()
    {
        var expectedInnerExceptionThrown = false;

        var builder = new HostBuilder()
            .ConfigureLogging((logging) => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
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

        using var host = builder.Build();

        await Assert.ThrowsAsync<NotSupportedException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        await host.StopAsync(TestContext.Current.CancellationToken);

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

        var testRun = false;

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
        var configureBuilderCalled = false;

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

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(configureBuilderCalled);

        await host.StopAsync(TestContext.Current.CancellationToken);

        host.Dispose();
    }

    [Fact]
    public void AddOpenTelemetry_WithTracing_NestedResolutionUsingConfigureTest()
    {
        var innerTestExecuted = false;

        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithTracing(builder =>
        {
            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    innerTestExecuted = true;
#if NET10_0
                    Assert.Throws<NotSupportedException>(() => sp.GetService<TracerProvider>());
#else
                    Assert.Throws<InvalidOperationException>(() => sp.GetService<TracerProvider>());
#endif
                });
            }
        });

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedProvider = serviceProvider.GetRequiredService<TracerProvider>();
        Assert.True(innerTestExecuted);
    }

#if NET11_0_OR_GREATER
    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 1)]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingTracingBuilderTest(
        bool enableSource,
        bool alsoAddSource,
        int expectedActivities)
    {
        var sourceName = Utils.GetCurrentMethodName();
        List<Activity> exportedItems = [];

        var services = new ServiceCollection();

        services.AddTracing(builder =>
        {
            if (enableSource)
            {
                builder.EnableTracing(sourceName);
            }
        });

        services.AddOpenTelemetry().WithTracing(builder =>
        {
            builder.AddInMemoryExporter(exportedItems);

            if (alsoAddSource)
            {
                // Enabling the same source both ways must not process each Activity twice
                builder.AddSource(sourceName);
            }
        });

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource(sourceName);

        using (var activity = source.StartActivity("TestActivity"))
        {
            Assert.Equal(expectedActivities > 0, activity?.IsAllDataRequested ?? false);
        }

        tracerProvider.ForceFlush();

        Assert.Equal(expectedActivities, exportedItems.Count);
    }

    [Fact]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingTracingBuilderHonorsSamplerTest()
    {
        var sourceName = Utils.GetCurrentMethodName();
        List<Activity> exportedItems = [];

        var services = new ServiceCollection();

        services.AddTracing(builder => builder.EnableTracing(sourceName));

        services.AddOpenTelemetry().WithTracing(builder => builder
            .SetSampler(new AlwaysOffSampler())
            .AddInMemoryExporter(exportedItems));

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource(sourceName);

        using (var activity = source.StartActivity("TestActivity"))
        {
            Assert.False(activity?.IsAllDataRequested ?? false);
        }

        tracerProvider.ForceFlush();

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingConfigurationTest()
    {
        var sourceName = Utils.GetCurrentMethodName();
        List<Activity> exportedItems = [];

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tracing:EnabledTracing:Default"] = "false",
                [$"Tracing:EnabledTracing:{sourceName}"] = "true",
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddTracing(builder => builder.AddConfiguration(configuration.GetSection("Tracing")));
        services.AddOpenTelemetry().WithTracing(builder => builder.AddInMemoryExporter(exportedItems));

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        using var enabledSource = new ActivitySource(sourceName);
        using var disabledSource = new ActivitySource("Disabled." + sourceName);

        using (var activity = enabledSource.StartActivity("Enabled"))
        {
            Assert.NotNull(activity);
        }

        Assert.Null(disabledSource.StartActivity("Disabled"));

        tracerProvider.ForceFlush();

        var exported = Assert.Single(exportedItems);
        Assert.Equal("Enabled", exported.OperationName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingTracingBuilderProcessorDependingOnFactoryTest(bool resolveFactoryFirst)
    {
        var sourceName = Utils.GetCurrentMethodName();

        var services = new ServiceCollection();

        services.AddTracing(builder => builder.EnableTracing(sourceName));
        services.AddOpenTelemetry().WithTracing(builder => builder.AddProcessor<FactoryDependentProcessor>());

        using var serviceProvider = services.BuildServiceProvider();

        if (resolveFactoryFirst)
        {
            _ = serviceProvider.GetRequiredService<ActivitySourceFactory>();
        }

        _ = serviceProvider.GetRequiredService<TracerProvider>();

        var processor = serviceProvider.GetRequiredService<FactoryDependentProcessor>();

        using var source = new ActivitySource(sourceName);

        using (var activity = source.StartActivity("TestActivity"))
        {
            Assert.NotNull(activity);
        }

        Assert.Equal(1, processor.Started);
        Assert.Equal(1, processor.Ended);
    }

    [Fact]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingTracingBuilderDetachesWhenFactoryFailsTest()
    {
        var sourceName = Utils.GetCurrentMethodName();

        var services = new ServiceCollection();

        services.AddTracing(builder => builder.EnableTracing(sourceName));
        services.AddSingleton<ActivitySourceFactory>(static _ => throw new InvalidOperationException("The factory failed."));
        services.AddOpenTelemetry().WithTracing(builder => builder.AddSource(sourceName + ".Other"));

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<TracerProvider>());

        var bridge = serviceProvider.GetRequiredService<OpenTelemetryTracingListener.TracerProviderBridge>();

        using var source = new ActivitySource(sourceName);

        Assert.Null(bridge.GetProviderFor(source));
    }

    [Fact]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingTracingBuilderDoesNotProcessLegacyActivitiesTest()
    {
        var sourceName = Utils.GetCurrentMethodName();
        using var processor = new CountingProcessor();

        var services = new ServiceCollection();

        // Enable all sources, which includes the (unnamed) source used by legacy activities
        services.AddTracing(builder => builder.EnableTracing());
        services.AddOpenTelemetry().WithTracing(builder => builder
            .SetSampler(new AlwaysOffSampler())
            .AddProcessor(processor));

        using var serviceProvider = services.BuildServiceProvider();

        _ = serviceProvider.GetRequiredService<TracerProvider>();

        using var legacy = new Activity(sourceName);
        legacy.Start();
        legacy.Stop();

        Assert.Equal(0, processor.Started);
        Assert.Equal(0, processor.Ended);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddOpenTelemetry_WithTracing_ActivitySourcesEnabledUsingTracingBuilderStopsWhenProviderIsStoppedTest(bool dispose)
    {
        var sourceName = Utils.GetCurrentMethodName();
        List<Activity> exportedItems = [];
        using var sampler = new CountingSampler();

        var services = new ServiceCollection();

        services.AddTracing(builder => builder.EnableTracing(sourceName));
        services.AddOpenTelemetry().WithTracing(builder => builder
            .SetSampler(sampler)
            .AddInMemoryExporter(exportedItems));

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        // Keep the factory (and so its listener) alive for the remainder of the test
        _ = serviceProvider.GetRequiredService<ActivitySourceFactory>();

        using var source = new ActivitySource(sourceName);

        using (var activity = source.StartActivity("Before"))
        {
            Assert.NotNull(activity);
        }

        Assert.Single(exportedItems);
        Assert.Equal(1, sampler.Calls);

        if (dispose)
        {
            tracerProvider.Dispose();
        }
        else
        {
            Assert.True(tracerProvider.Shutdown());
        }

        using (var activity = source.StartActivity("After"))
        {
            Assert.Null(activity);
        }

        Assert.Single(exportedItems);
        Assert.Equal(1, sampler.Calls);
    }
#endif

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

        var testRun = false;

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
        var configureBuilderCalled = false;

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

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(configureBuilderCalled);

        await host.StopAsync(TestContext.Current.CancellationToken);

        host.Dispose();
    }

    [Fact]
    public void AddOpenTelemetry_WithMetrics_NestedResolutionUsingConfigureTest()
    {
        var innerTestExecuted = false;

        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithMetrics(builder =>
        {
            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    innerTestExecuted = true;
#if NET10_0
                    Assert.Throws<NotSupportedException>(() => sp.GetService<MeterProvider>());
#else
                    Assert.Throws<InvalidOperationException>(() => sp.GetService<MeterProvider>());
#endif
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

        var testRun = false;

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
        var configureBuilderCalled = false;

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
        var innerTestExecuted = false;

        var services = new ServiceCollection();

        services.AddOpenTelemetry().WithLogging(builder =>
        {
            if (builder is IDeferredLoggerProviderBuilder deferredLoggerProviderBuilder)
            {
                deferredLoggerProviderBuilder.Configure((sp, builder) =>
                {
                    innerTestExecuted = true;
#if NET10_0
                    Assert.Throws<NotSupportedException>(() => sp.GetService<LoggerProvider>());
#else
                    Assert.Throws<InvalidOperationException>(() => sp.GetService<LoggerProvider>());
#endif
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
        // Arrange
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

        // Act
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var service = host.Services
            .GetServices<IHostedService>()
            .OfType<TestHostedService>()
            .Single();

        // Give the background service some time to run.
        await Task.WhenAny(service.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None));

        await host.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var activity = Assert.Single(exportedItems);
        Assert.Equal("test", activity.DisplayName);
    }

    [Fact]
    public void AddOpenTelemetry_TelemetryHostInitializer_ResolvesConfiguredProviders()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithTracing(builder => { })
            .WithMetrics(builder => { })
            .WithLogging(builder => { });

        using var serviceProvider = services.BuildServiceProvider();

        var initializer = serviceProvider.GetRequiredService<ITelemetryHostInitializer>();
        initializer.Initialize();

        Assert.NotNull(serviceProvider.GetService<TracerProvider>());
        Assert.NotNull(serviceProvider.GetService<MeterProvider>());
        Assert.NotNull(serviceProvider.GetService<LoggerProvider>());
    }

    [Fact]
    public void AddOpenTelemetry_TelemetryHostInitializer_WarnsWhenProvidersNotRegistered()
    {
        using var eventListener = new TestEventListener(HostingExtensionsEventSource.Log);

        var services = new ServiceCollection();

        services.AddOpenTelemetry();

        using var serviceProvider = services.BuildServiceProvider();

        var initializer = serviceProvider.GetRequiredService<ITelemetryHostInitializer>();
        initializer.Initialize();

        Assert.Single(eventListener.Messages, e => e.EventId == 1);
        Assert.Single(eventListener.Messages, e => e.EventId == 2);
        Assert.Single(eventListener.Messages, e => e.EventId == 3);
    }

    [Fact]
    public async Task AddOpenTelemetry_CustomTelemetryHostInitializer_InvokedByHostedServiceOnStart()
    {
        var customInitializer = new CustomTelemetryHostInitializer();

        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton<ITelemetryHostInitializer>(customInitializer);
            services.AddOpenTelemetry();
        });

        using var host = builder.Build();

        Assert.Same(customInitializer, host.Services.GetRequiredService<ITelemetryHostInitializer>());
        Assert.False(customInitializer.Initialized, "Custom initializer was invoked.");

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(customInitializer.Initialized, "Custom initializer was not invoked.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddOpenTelemetry_TelemetryIsCapturedDuringStartingAsync()
    {
        var exportedItems = new List<Activity>();

        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddHostedService<TestHostedLifecycleService>();
            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder.SetSampler(new AlwaysOnSampler());
                    builder.AddSource(nameof(TestHostedLifecycleService));
                    builder.AddInMemoryExporter(exportedItems);
                });
        });

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        var activity = Assert.Single(exportedItems);
        Assert.Equal("starting", activity.DisplayName);
    }

    [Fact]
    public async Task AddOpenTelemetry_TelemetryHostInitializer_InvokedOnceOnStart()
    {
        var customInitializer = new CustomTelemetryHostInitializer();

        var builder = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton<ITelemetryHostInitializer>(customInitializer);
            services.AddOpenTelemetry();
        });

        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, customInitializer.InitializeCount);
    }

    [Fact]
    public async Task TelemetryHostedService_StartAsync_InitializesWhenStartingAsyncNotCalled()
    {
        var customInitializer = new CustomTelemetryHostInitializer();
        var service = new TelemetryHostedService(customInitializer);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, customInitializer.InitializeCount);
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

#if NET11_0_OR_GREATER
    private class CountingProcessor : BaseProcessor<Activity>
    {
        public int Started { get; private set; }

        public int Ended { get; private set; }

        public override void OnStart(Activity data) => this.Started++;

        public override void OnEnd(Activity data) => this.Ended++;
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class FactoryDependentProcessor(ActivitySourceFactory factory) : CountingProcessor
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        public ActivitySourceFactory Factory { get; } = factory;
    }

    private sealed class CountingSampler : Sampler, IDisposable
    {
        private bool disposed;

        public int Calls { get; private set; }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            Assert.False(this.disposed);
            this.Calls++;
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        public void Dispose() => this.disposed = true;
    }
#endif

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class TestHostedLifecycleService : IHostedLifecycleService, IDisposable
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        private readonly ActivitySource activitySource = new(nameof(TestHostedLifecycleService));

        public Task StartingAsync(CancellationToken cancellationToken)
        {
            using (this.activitySource.StartActivity("starting"))
            {
            }

            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() => this.activitySource.Dispose();
    }

    private sealed class CustomTelemetryHostInitializer : ITelemetryHostInitializer
    {
        public int InitializeCount { get; private set; }

        public bool Initialized => this.InitializeCount > 0;

        public void Initialize() => this.InitializeCount++;
    }
}
