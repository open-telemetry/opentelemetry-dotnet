// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public class TracerProviderBuilderExtensionsTests
{
    [Fact]
    public void SetErrorStatusOnExceptionEnabled()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .SetErrorStatusOnException(false)
            .SetErrorStatusOnException(false)
            .SetErrorStatusOnException(true)
            .SetErrorStatusOnException(true)
            .SetErrorStatusOnException(false)
            .SetErrorStatusOnException()
            .Build();

        Activity? activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new InvalidOperationException("Oops!");
            }
        }
        catch (Exception)
        {
        }

        Assert.NotNull(activity);
        Assert.Equal(StatusCode.Error, activity.GetStatus().StatusCode);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public void SetErrorStatusOnExceptionDisabled()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .SetErrorStatusOnException()
            .SetErrorStatusOnException(false)
            .Build();

        Activity? activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new InvalidOperationException("Oops!");
            }
        }
        catch (Exception)
        {
        }

        Assert.NotNull(activity);
        Assert.Equal(StatusCode.Unset, activity.GetStatus().StatusCode);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public void SetErrorStatusOnExceptionDefault()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        Activity? activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new InvalidOperationException("Oops!");
            }
        }
        catch (Exception)
        {
        }

        Assert.NotNull(activity);
        Assert.Equal(StatusCode.Unset, activity.GetStatus().StatusCode);
    }

    [Fact]
    public void ServiceLifecycleAvailableToSDKBuilderTest()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        MyInstrumentation? myInstrumentation = null;

        RunBuilderServiceLifecycleTest(
            builder,
            () =>
            {
                var provider = builder.Build() as TracerProviderSdk;

                // Note: Build can only be called once
                Assert.Throws<NotSupportedException>(() => builder.Build());

                Assert.NotNull(provider);
                Assert.NotNull(provider.OwnedServiceProvider);

                myInstrumentation = ((IServiceProvider)provider.OwnedServiceProvider).GetRequiredService<MyInstrumentation>();

                return provider;
            },
            provider =>
            {
                provider.Dispose();
            });

        Assert.NotNull(myInstrumentation);
        Assert.True(myInstrumentation.Disposed);
    }

    [Fact]
    public void AddProcessorTest()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        List<MyProcessor> processorsToAdd =
        [
            new MyProcessor()
            {
                Name = "A",
            },
            new MyProcessor()
            {
                Name = "B",
            },
            new MyProcessor()
            {
                Name = "C",
            },
        ];
#pragma warning restore CA2000 // Dispose objects before losing scope

        var builder = Sdk.CreateTracerProviderBuilder();
        foreach (var processor in processorsToAdd)
        {
            builder.AddProcessor(processor);
        }

        List<MyProcessor> expectedProcessors =
        [
            processorsToAdd.First(p => p.Name == "A"),
            processorsToAdd.First(p => p.Name == "B"),
            processorsToAdd.First(p => p.Name == "C"),
        ];

        List<MyProcessor> actualProcessors = [];

        using (var provider = builder.Build() as TracerProviderSdk)
        {
            Assert.NotNull(provider);
            Assert.NotNull(provider.Processor);

            var compositeProcessor = provider.Processor as CompositeProcessor<Activity>;

            Assert.NotNull(compositeProcessor);

            var current = compositeProcessor.Head;
            while (current != null)
            {
                var processor = current.Value as MyProcessor;
                Assert.NotNull(processor);

                actualProcessors.Add(processor);
                Assert.False(processor.Disposed);

                current = current.Next;
            }

            Assert.Equal(expectedProcessors, actualProcessors);
        }

        foreach (var processor in actualProcessors)
        {
            Assert.True(processor.Disposed);
        }
    }

    [Fact]
    public void AddProcessorWithWeightTest()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        List<MyProcessor> processorsToAdd =
        [
            new MyProcessor()
            {
                Name = "C",
                PipelineWeight = 0,
            },
            new MyProcessor()
            {
                Name = "E",
                PipelineWeight = 10_000,
            },
            new MyProcessor()
            {
                Name = "B",
                PipelineWeight = -10_000,
            },
            new MyProcessor()
            {
                Name = "F",
                PipelineWeight = int.MaxValue,
            },
            new MyProcessor()
            {
                Name = "A",
                PipelineWeight = int.MinValue,
            },
            new MyProcessor()
            {
                Name = "D",
                PipelineWeight = 0,
            },
        ];
#pragma warning restore CA2000 // Dispose objects before losing scope

        var builder = Sdk.CreateTracerProviderBuilder();
        foreach (var processor in processorsToAdd)
        {
            builder.AddProcessor(processor);
        }

        List<MyProcessor> expectedProcessors =
        [
            processorsToAdd.First(p => p.Name == "A"),
            processorsToAdd.First(p => p.Name == "B"),
            processorsToAdd.First(p => p.Name == "C"),
            processorsToAdd.First(p => p.Name == "D"),
            processorsToAdd.First(p => p.Name == "E"),
            processorsToAdd.First(p => p.Name == "F"),
        ];

        List<MyProcessor> actualProcessors = [];

        using (var provider = builder
            .SetErrorStatusOnException() // Forced to be first processor
            .Build() as TracerProviderSdk)
        {
            Assert.NotNull(provider);
            Assert.NotNull(provider.Processor);

            var compositeProcessor = provider.Processor as CompositeProcessor<Activity>;

            Assert.NotNull(compositeProcessor);

            var isFirstProcessor = true;
            var lastWeight = int.MinValue;
            var current = compositeProcessor.Head;
            while (current != null)
            {
                if (isFirstProcessor)
                {
                    Assert.True(current.Value is ExceptionProcessor);
                    Assert.Equal(0, current.Value.PipelineWeight);
                    isFirstProcessor = false;
                }
                else
                {
                    var processor = current.Value as MyProcessor;
                    Assert.NotNull(processor);

                    actualProcessors.Add(processor);
                    Assert.False(processor.Disposed);

                    Assert.True(processor.PipelineWeight >= lastWeight);

                    lastWeight = processor.PipelineWeight;
                }

                current = current.Next;
            }

            Assert.Equal(expectedProcessors, actualProcessors);
        }

        foreach (var processor in actualProcessors)
        {
            Assert.True(processor.Disposed);
        }
    }

    [Fact]
    public void AddProcessorUsingDependencyInjectionTest()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        builder.AddProcessor<MyProcessor>();
        builder.AddProcessor<MyProcessor>();

        using var provider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(provider);
        Assert.NotNull(provider.OwnedServiceProvider);

        var processors = ((IServiceProvider)provider.OwnedServiceProvider).GetServices<MyProcessor>();

        // Note: Two "Add" calls but it is a singleton so only a single registration is produced
        Assert.Single(processors);

        var processor = provider.Processor as CompositeProcessor<Activity>;

        Assert.NotNull(processor);

        // Note: Two "Add" calls due yield two processors added to provider, even though they are the same
        Assert.True(processor.Head.Value is MyProcessor);
        Assert.True(processor.Head.Next?.Value is MyProcessor);
    }

    [Fact]
    public void AddInstrumentationTest()
    {
        List<object>? instrumentation = null;

        using (var provider = Sdk.CreateTracerProviderBuilder()
            .AddInstrumentation<MyInstrumentation>()
            .AddInstrumentation((sp, provider) => new MyInstrumentation() { Provider = provider })
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddInstrumentation(new MyInstrumentation())
#pragma warning restore CA2000 // Dispose objects before losing scope
            .AddInstrumentation(() => (object?)null)
            .Build() as TracerProviderSdk)
        {
            Assert.NotNull(provider);

            Assert.Equal(3, provider.Instrumentations.Count);

            Assert.Null(((MyInstrumentation)provider.Instrumentations[0]).Provider);
            Assert.False(((MyInstrumentation)provider.Instrumentations[0]).Disposed);

            Assert.NotNull(((MyInstrumentation)provider.Instrumentations[1]).Provider);
            Assert.False(((MyInstrumentation)provider.Instrumentations[1]).Disposed);

            Assert.Null(((MyInstrumentation)provider.Instrumentations[2]).Provider);
            Assert.False(((MyInstrumentation)provider.Instrumentations[2]).Disposed);

            instrumentation = [.. provider.Instrumentations];
        }

        Assert.NotNull(instrumentation);
        Assert.True(((MyInstrumentation)instrumentation[0]).Disposed);
        Assert.True(((MyInstrumentation)instrumentation[1]).Disposed);
        Assert.True(((MyInstrumentation)instrumentation[2]).Disposed);
    }

    [Fact]
    public void SetAndConfigureResourceTest()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        var configureInvocations = 0;
        var serviceProviderTestExecuted = false;

        builder.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("Test"));
        builder.ConfigureResource(builder =>
        {
            configureInvocations++;

            Assert.Single(builder.ResourceDetectors);

            builder.AddAttributes(new Dictionary<string, object>() { ["key1"] = "value1" });

            Assert.Equal(2, builder.ResourceDetectors.Count);
        });
        builder.SetResourceBuilder(ResourceBuilder.CreateEmpty());
        builder.ConfigureResource(builder =>
        {
            configureInvocations++;

            Assert.Empty(builder.ResourceDetectors);

            builder.AddDetectorInternal(sp =>
            {
                serviceProviderTestExecuted = true;
                Assert.NotNull(sp);
                return new ResourceBuilder.WrapperResourceDetector(new Resource(new Dictionary<string, object>() { ["key2"] = "value2" }));
            });

            Assert.Single(builder.ResourceDetectors);
        });

        using var provider = builder.Build() as TracerProviderSdk;

        Assert.True(serviceProviderTestExecuted);
        Assert.Equal(2, configureInvocations);
        Assert.NotNull(provider);
        Assert.Single(provider.Resource.Attributes);
        Assert.Contains(provider.Resource.Attributes, kvp => kvp.Key == "key2" && (string)kvp.Value == "value2");
    }

    [Fact]
    public void ConfigureBuilderIConfigurationAvailableTest()
    {
        Environment.SetEnvironmentVariable("TEST_KEY", "TEST_KEY_VALUE");

        var configureBuilderCalled = false;

        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureBuilder((sp, builder) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                configureBuilderCalled = true;

                var testKeyValue = configuration.GetValue<string?>("TEST_KEY", null);

                Assert.Equal("TEST_KEY_VALUE", testKeyValue);
            })
            .Build();

        Assert.True(configureBuilderCalled);

        Environment.SetEnvironmentVariable("TEST_KEY", null);
    }

    [Fact]
    public void ConfigureBuilderIConfigurationModifiableTest()
    {
        var configureBuilderCalled = false;

        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["TEST_KEY_2"] = "TEST_KEY_2_VALUE" })
                    .Build();

                services.AddSingleton<IConfiguration>(configuration);
            })
            .ConfigureBuilder((sp, builder) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                configureBuilderCalled = true;

                var testKey2Value = configuration.GetValue<string?>("TEST_KEY_2", null);

                Assert.Equal("TEST_KEY_2_VALUE", testKey2Value);
            })
            .Build();

        Assert.True(configureBuilderCalled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TracerProviderNestedResolutionUsingBuilderTest(bool callNestedConfigure)
    {
        var innerConfigureBuilderTestExecuted = false;
        var innerConfigureOpenTelemetryLoggerProviderTestExecuted = false;
        var innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted = false;

        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                if (callNestedConfigure)
                {
                    services.ConfigureOpenTelemetryTracerProvider(
                        builder =>
                        {
                            innerConfigureOpenTelemetryLoggerProviderTestExecuted = true;
                            builder.AddInstrumentation<MyInstrumentation>();
                        });
                    services.ConfigureOpenTelemetryTracerProvider(
                        (sp, builder) =>
                        {
                            innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted = true;
                            Assert.Throws<NotSupportedException>(() => builder.AddInstrumentation<MyInstrumentation>());
                        });
                }
            })
            .ConfigureBuilder((sp, builder) =>
            {
                innerConfigureBuilderTestExecuted = true;
                Assert.Throws<NotSupportedException>(() => sp.GetService<TracerProvider>());
            })
            .Build() as TracerProviderSdk;

        Assert.NotNull(provider);

        Assert.True(innerConfigureBuilderTestExecuted);
        Assert.Equal(callNestedConfigure, innerConfigureOpenTelemetryLoggerProviderTestExecuted);
        Assert.Equal(callNestedConfigure, innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted);

        if (callNestedConfigure)
        {
            Assert.Single(provider.Instrumentations);
        }
        else
        {
            Assert.Empty(provider.Instrumentations);
        }

        Assert.Throws<NotSupportedException>(() => provider.GetServiceProvider()?.GetService<TracerProvider>());
    }

    [Fact]
    public void TracerProviderSetSamplerFactoryTest()
    {
        var factoryInvoked = false;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sp =>
            {
                factoryInvoked = true;

                Assert.NotNull(sp);

                return new MySampler();
            })
            .Build() as TracerProviderSdk;

        Assert.True(factoryInvoked);

        Assert.NotNull(tracerProvider);
        Assert.True(tracerProvider.Sampler is MySampler);
    }

    [Theory]
    [InlineData(null, null, false, "ParentBased{AlwaysOnSampler}")]
    [InlineData("always_off", null, false, "AlwaysOffSampler")]
    [InlineData("parentbased_traceidratio", "0.25", false, "ParentBased{TraceIdRatioBasedSampler{0.250000}}")]
    [InlineData("always_off", null, true, "MySampler")]
    [InlineData(null, null, true, "MySampler")]
    public void ConfigureSamplerNotRegisteredLeavesResolutionUnchanged(
        string? samplerConfigValue,
        string? samplerArgConfigValue,
        bool setSamplerProgrammatically,
        string expectedDescription)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSamplerConfiguration(samplerConfigValue, samplerArgConfigValue);

        if (setSamplerProgrammatically)
        {
            builder.SetSampler(new MySampler());
        }

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal(expectedDescription, tracerProvider.Sampler.Description);
    }

    [Fact]
    public void ConfigureSamplerWrapsResolvedSampler()
    {
        var resolvedSampler = new MySampler();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(resolvedSampler)
            .ConfigureSampler((sp, sampler) => new MyWrappingSampler(sampler))
            .Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);

        var wrapper = Assert.IsType<MyWrappingSampler>(tracerProvider.Sampler);
        Assert.Same(resolvedSampler, wrapper.Inner);
    }

    [Fact]
    public void ConfigureSamplerReturnedWrapperControlsActivitySampling()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOffSampler())
            .ConfigureSampler((sp, sampler) => new MyWrappingSampler(new AlwaysOnSampler()))
            .Build();

        using var activity = activitySource.StartActivity("Activity");

        Assert.NotNull(activity);
        Assert.True(activity.IsAllDataRequested);
        Assert.True(activity.Recorded);
    }

    [Fact]
    public void ConfigureSamplerReturnedBuiltInSamplerControlsActivitySampling()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .ConfigureSampler((sp, sampler) => new AlwaysOffSampler())
            .Build();

        using var activity = activitySource.StartActivity("Activity");

        Assert.NotNull(activity);
        Assert.False(activity.IsAllDataRequested);
        Assert.False(activity.Recorded);
    }

    [Fact]
    public void ConfigureSamplerReceivesSamplerResolvedFromConfiguration()
    {
        Sampler? receivedSampler = null;

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSamplerConfiguration("parentbased_traceidratio", "0.5");

        builder.ConfigureSampler((sp, sampler) =>
        {
            receivedSampler = sampler;
            return sampler;
        });

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.NotNull(receivedSampler);
        Assert.IsType<ParentBasedSampler>(receivedSampler);
        Assert.Equal("ParentBased{TraceIdRatioBasedSampler{0.500000}}", receivedSampler.Description);
        Assert.Same(receivedSampler, tracerProvider.Sampler);
    }

    [Theory]
    [InlineData(false, "instance")]
    [InlineData(true, "instance")]
    [InlineData(false, "type")]
    [InlineData(true, "type")]
    [InlineData(false, "factory")]
    [InlineData(true, "factory")]
    public void ConfigureSamplerOverridesSetSamplerRegardlessOfRegistrationOrder(bool configureSamplerFirst, string setSamplerOverload)
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        Sampler? receivedSampler = null;
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .AddSamplerConfiguration("always_on");

        if (configureSamplerFirst)
        {
            RegisterConfigurator();
        }

        switch (setSamplerOverload)
        {
            case "instance":
                builder.SetSampler(new MySampler());
                break;
            case "type":
                builder.SetSampler<MySampler>();
                break;
            case "factory":
                builder.SetSampler(sp => new MySampler());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(setSamplerOverload));
        }

        if (!configureSamplerFirst)
        {
            RegisterConfigurator();
        }

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.IsType<MySampler>(receivedSampler);
        Assert.IsType<AlwaysOffSampler>(tracerProvider.Sampler);

        using var activity = activitySource.StartActivity("Activity");

        Assert.NotNull(activity);
        Assert.False(activity.Recorded);

        void RegisterConfigurator()
        {
            builder.ConfigureSampler((sp, sampler) =>
            {
                receivedSampler = sampler;
                return new AlwaysOffSampler();
            });
        }
    }

    [Fact]
    public void ConfigureSamplerReceivesDefaultSamplerWhenNothingElseIsConfigured()
    {
        Sampler? receivedSampler = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureSampler((sp, sampler) =>
            {
                receivedSampler = sampler;
                return sampler;
            })
            .Build();

        Assert.NotNull(receivedSampler);
        Assert.IsType<ParentBasedSampler>(receivedSampler);
        Assert.Equal("ParentBased{AlwaysOnSampler}", receivedSampler.Description);
    }

    [Fact]
    public void ConfigureSamplerCallbacksAreChainedInRegistrationOrder()
    {
        var resolvedSampler = new MySampler();

        var receivedSamplers = new List<Sampler>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(resolvedSampler)
            .ConfigureSampler((sp, sampler) =>
            {
                receivedSamplers.Add(sampler);
                return new MyWrappingSampler(sampler);
            })
            .ConfigureSampler((sp, sampler) =>
            {
                receivedSamplers.Add(sampler);
                return new MyWrappingSampler(sampler);
            })
            .Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal(2, receivedSamplers.Count);
        Assert.Same(resolvedSampler, receivedSamplers[0]);

        var innerWrapper = Assert.IsType<MyWrappingSampler>(receivedSamplers[1]);
        Assert.Same(resolvedSampler, innerWrapper.Inner);

        var outerWrapper = Assert.IsType<MyWrappingSampler>(tracerProvider.Sampler);
        Assert.Same(innerWrapper, outerWrapper.Inner);
    }

    [Fact]
    public void ConfigureSamplerCallbacksEmitDiagnosticsInRegistrationOrder()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new MySampler())
            .ConfigureSampler((sp, sampler) => new MyWrappingSampler(sampler))
            .ConfigureSampler((sp, sampler) => sampler)
            .Build();

        var messages = eventListener.Messages
            .Where(e => e.EventId == 46)
            .Select(e => e.Payload?[0] as string);

        Assert.Contains(
            $"Sampler configurator 1 of 2 changed sampler from \"{typeof(MySampler)}\" to \"{typeof(MyWrappingSampler)}\".",
            messages);
        Assert.Contains(
            $"Sampler configurator 2 of 2 left sampler \"{typeof(MyWrappingSampler)}\" unchanged.",
            messages);
    }

    [Fact]
    public void ConfigureSamplerReturningInputLeavesSamplerUnchanged()
    {
        var resolvedSampler = new MySampler();

        var invocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(resolvedSampler)
            .ConfigureSampler((sp, sampler) =>
            {
                invocations++;
                return sampler;
            })
            .Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal(1, invocations);
        Assert.Same(resolvedSampler, tracerProvider.Sampler);
    }

    [Fact]
    public void ConfigureSamplerReturningNullFailsBuild()
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureSampler((sp, sampler) => null!);

        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void ConfigureBuilderThrowingBeforeSamplerIsResolvedFailsBuild()
    {
        // The provider has no sampler to shut down or dispose when cleaning up after the failure
        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureBuilder((sp, builder) => throw new InvalidOperationException("test exception"));

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("test exception", exception.Message);
    }

    [Fact]
    public void ConfigureSamplerThrowingFailsBuild()
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureSampler((sp, sampler) => throw new InvalidOperationException("test exception"));

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("test exception", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigureSamplerDisposesIntermediateSamplerWhenLaterCallbackFails(bool callbackReturnsNull)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope - failed provider build should dispose the sampler
        var intermediateSampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureSampler((sp, sampler) => intermediateSampler)
            .ConfigureSampler((sp, sampler) => callbackReturnsNull
                ? null!
                : throw new InvalidOperationException("test exception"));

        Assert.Throws<InvalidOperationException>(builder.Build);
        Assert.Equal(1, intermediateSampler.ShutdownCalls);
        Assert.True(intermediateSampler.ShutdownBeforeDispose);
        Assert.True(intermediateSampler.Disposed);
    }

    [Fact]
    public void ConfigureSamplerThrowsWhenCallbackIsNull()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.ConfigureSampler(null!));
    }

    [Fact]
    public void ConfigureSamplerReceivesServiceProvider()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        builder.ConfigureServices(services => services.TryAddSingleton<MySampler>());

        Sampler? samplerFromServices = null;

        builder.ConfigureSampler((sp, sampler) =>
        {
            Assert.NotNull(sp);

            samplerFromServices = sp.GetRequiredService<MySampler>();

            return samplerFromServices;
        });

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.NotNull(samplerFromServices);
        Assert.Same(samplerFromServices, tracerProvider.Sampler);
    }

    [Fact]
    public void ConfigureSamplerReturnedSamplerIsDisposedByProvider()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope - disposal is what the test asserts
        var resolvedSampler = new MyDisposableSampler();
        var returnedSampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(resolvedSampler)
            .ConfigureSampler((sp, sampler) => returnedSampler)
            .Build();

        Assert.False(returnedSampler.Disposed);

        tracerProvider.Dispose();

        Assert.True(returnedSampler.Disposed);
        Assert.Equal(1, returnedSampler.ShutdownCalls);

        // Note: The SDK only shuts down and disposes the sampler it ends up holding.
        // Disposing a sampler which was replaced is the responsibility of the callback.
        Assert.False(resolvedSampler.Disposed);
        Assert.Equal(0, resolvedSampler.ShutdownCalls);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(60_000)]
    public void TracerProviderShutdownShutsDownSamplerOnceAndDoesNotCallItAfterwards(int timeoutMilliseconds)
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

#pragma warning disable CA2000 // Dispose objects before losing scope - disposal is what the test asserts
        var sampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .Build();

        using (var activity = activitySource.StartActivity("before"))
        {
            Assert.NotNull(activity);
        }

        Assert.Equal(1, sampler.ShouldSampleCalls);
        Assert.Equal(0, sampler.ShutdownCalls);

        Assert.True(tracerProvider.Shutdown(timeoutMilliseconds));

        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.False(sampler.Disposed);

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            Assert.Equal(Timeout.Infinite, sampler.ShutdownTimeout);
        }
        else
        {
            // The time spent shutting down the processors and listener is not available to the sampler
            Assert.InRange(sampler.ShutdownTimeout, 0, timeoutMilliseconds);
        }

        using (var activity = activitySource.StartActivity("after"))
        {
            Assert.Null(activity);
        }

        tracerProvider.Dispose();

        // Shutting down again via Dispose does not shut the sampler down a second time
        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.True(sampler.ShutdownBeforeDispose);
        Assert.True(sampler.Disposed);
        Assert.Equal(1, sampler.ShouldSampleCalls);
        Assert.False(sampler.ShouldSampleAfterShutdown);
    }

    [Theory]
    [InlineData("always_on")]
    [InlineData("always_off")]
    public void TracerProviderShutdownSucceedsForEveryProviderUsingASharedSampler(string samplerConfigValue)
    {
        // These samplers are process-wide instances, so shutting one provider down must not
        // make the shutdown of any other provider which was configured the same way fail.
        var first = Sdk.CreateTracerProviderBuilder().AddSamplerConfiguration(samplerConfigValue).Build();
        var second = Sdk.CreateTracerProviderBuilder().AddSamplerConfiguration(samplerConfigValue).Build();

        Assert.True(first.Shutdown());
        Assert.True(second.Shutdown());

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void TracerProviderShutdownDoesNotCallSamplerForActivitiesCapturedBeforeShutdown()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

#pragma warning disable CA2000 // Dispose objects before losing scope - disposal is what the test asserts
        var sampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        // A listener which is registered first and blocks while the activity is being created.
        // ActivitySource has already captured the listeners at this point, including the one
        // for the provider below, so disposing the provider's listener does not stop it being called.
        using var blockingListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
                return ActivitySamplingResult.None;
            },
        };

        ActivitySource.AddActivityListener(blockingListener);

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .Build();

        var thread = new Thread(() => activitySource.StartActivity("captured")?.Dispose());
        thread.Start();

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            Assert.True(tracerProvider.Shutdown());
        }
        finally
        {
            release.Set();
            thread.Join();
            tracerProvider.Dispose();
        }

        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.Equal(0, sampler.ShouldSampleCalls);
        Assert.False(sampler.ShouldSampleAfterShutdown);
    }

    [Fact]
    public void TracerProviderShutdownDoesNotCallSamplerForActivityWhichWasBeingCreatedBeforeShutdown()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

#pragma warning disable CA2000 // Dispose objects before losing scope - disposal is what the test asserts
        var sampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var blockedThreadId = 0;
        var originalGenerator = Activity.TraceIdGenerator;

        // Creating the parameters for the sampler generates the trace ID, which can run user code
        // Only block the thread this test uses, as the generator is shared with any other test
        Activity.TraceIdGenerator = () =>
        {
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref blockedThreadId))
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
            }

            return ActivityTraceId.CreateRandom();
        };

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .Build();

        var thread = new Thread(() =>
        {
            Volatile.Write(ref blockedThreadId, Environment.CurrentManagedThreadId);
            activitySource.StartActivity("blocked")?.Dispose();
        });

        thread.Start();

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            Assert.True(tracerProvider.Shutdown());
        }
        finally
        {
            release.Set();
            thread.Join();
            Activity.TraceIdGenerator = originalGenerator;
            tracerProvider.Dispose();
        }

        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.Equal(0, sampler.ShouldSampleCalls);
        Assert.False(sampler.ShouldSampleAfterShutdown);
    }

    [Fact]
    public void TracerProviderShutdownDoesNotCallSamplerForLegacyActivityCapturedBeforeShutdown()
    {
        var operationName = Utils.GetCurrentMethodName();

#pragma warning disable CA2000 // Dispose objects before losing scope - disposal is what the test asserts
        var sampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        // A listener which is registered first and blocks while the activity is being started.
        // Activities which have no source are all listened to through the same, default, source.
        using var blockingListener = new ActivityListener
        {
            ShouldListenTo = source => string.IsNullOrEmpty(source.Name),
            ActivityStarted = activity =>
            {
                if (activity.OperationName == operationName)
                {
                    entered.Set();
                    release.Wait(TimeSpan.FromSeconds(30));
                }
            },
        };

        ActivitySource.AddActivityListener(blockingListener);

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddLegacySource(operationName)
            .SetSampler(sampler)
            .Build();

        var isAllDataRequested = true;

        var thread = new Thread(() =>
        {
            using var activity = new Activity(operationName);
            activity.Start();
            isAllDataRequested = activity.IsAllDataRequested;
        });

        thread.Start();

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            Assert.True(tracerProvider.Shutdown());
        }
        finally
        {
            release.Set();
            thread.Join();
            tracerProvider.Dispose();
        }

        Assert.False(isAllDataRequested);
        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.Equal(0, sampler.ShouldSampleCalls);
        Assert.False(sampler.ShouldSampleAfterShutdown);
    }

    [Fact]
    public void TracerProviderDisposeShutsDownSamplerAfterProcessorsAndListener()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

#pragma warning disable CA2000 // Dispose objects before losing scope - disposal is what the test asserts
        var sampler = new MyDisposableSampler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        Activity? activityDuringProcessorShutdown = null;

        // Something which is created while the processors are shutting down is still sampled
        using var processor = new ShutdownCallbackProcessor(() =>
        {
            activityDuringProcessorShutdown = activitySource.StartActivity("during shutdown");
            activityDuringProcessorShutdown?.Dispose();
        });

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddProcessor(processor)
            .Build();

        Assert.Equal(0, sampler.ShutdownCalls);

        tracerProvider.Dispose();

        Assert.NotNull(activityDuringProcessorShutdown);
        Assert.Equal(1, sampler.ShouldSampleCalls);
        Assert.False(sampler.ShouldSampleAfterShutdown);
        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.True(sampler.ShutdownBeforeDispose);
        Assert.True(sampler.Disposed);
    }

    [Fact]
    public void TracerProviderAddProcessorFactoryTest()
    {
        var factoryInvoked = false;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(sp =>
            {
                factoryInvoked = true;

                Assert.NotNull(sp);

                return new MyProcessor();
            })
            .Build() as TracerProviderSdk;

        Assert.True(factoryInvoked);

        Assert.NotNull(tracerProvider);
        Assert.True(tracerProvider.Processor is MyProcessor);
    }

    [Fact]
    public void TracerProviderBuilderCustomImplementationBuildTest()
    {
        var builder = new MyTracerProviderBuilder();

        Assert.Throws<NotSupportedException>(builder.Build);
    }

    private static void RunBuilderServiceLifecycleTest(
        TracerProviderBuilder builder,
        Func<TracerProviderSdk> buildFunc,
        Action<TracerProviderSdk> postAction)
    {
        var baseBuilder = builder as TracerProviderBuilderBase;

        builder
            .AddSource("TestSource1")
            .AddLegacySource("TestLegacySource1")
            .SetSampler<MySampler>();

        var configureServicesCalled = false;
        builder.ConfigureServices(services =>
        {
            configureServicesCalled = true;

            Assert.NotNull(services);

            services.TryAddSingleton<MyInstrumentation>();
            services.TryAddSingleton<MyProcessor>();

            // Note: This is strange to call ConfigureOpenTelemetryTracerProvider here, but supported
            services.ConfigureOpenTelemetryTracerProvider((sp, b) =>
            {
                Assert.Throws<NotSupportedException>(() => b.ConfigureServices(services => { }));

                b.AddInstrumentation(sp.GetRequiredService<MyInstrumentation>());
            });
        });

        var configureBuilderInvocations = 0;
        builder.ConfigureBuilder((sp, builder) =>
        {
            configureBuilderInvocations++;

            var sdkBuilder = builder as TracerProviderBuilderSdk;
            Assert.NotNull(sdkBuilder);

            builder
                .AddSource("TestSource2")
                .AddLegacySource("TestLegacySource2");

            Assert.Contains(sdkBuilder.Sources, s => s == "TestSource1");
            Assert.Contains(sdkBuilder.Sources, s => s == "TestSource2");
            Assert.Contains(sdkBuilder.LegacyActivityOperationNames, s => s == "TestLegacySource1");
            Assert.Contains(sdkBuilder.LegacyActivityOperationNames, s => s == "TestLegacySource2");

            // Note: Services can't be configured at this stage
            Assert.Throws<NotSupportedException>(
                () => builder.ConfigureServices(services => services.TryAddSingleton<TracerProviderBuilderExtensionsTests>()));

            builder.AddProcessor(sp.GetRequiredService<MyProcessor>());

            builder.ConfigureBuilder((_, b) =>
            {
                // Note: ConfigureBuilder calls can be nested, this is supported
                configureBuilderInvocations++;

                b.ConfigureBuilder((_, _) =>
                {
                    configureBuilderInvocations++;
                });
            });
        });

        var provider = buildFunc();

        Assert.True(configureServicesCalled);
        Assert.Equal(3, configureBuilderInvocations);

        Assert.True(provider.Sampler is MySampler);
        var instrumentation = Assert.Single(provider.Instrumentations);
        Assert.True(instrumentation is MyInstrumentation);
        Assert.True(provider.Processor is MyProcessor);

        postAction(provider);
    }

    private sealed class MySampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(SamplingDecision.RecordAndSample);
    }

    private sealed class MyWrappingSampler : Sampler
    {
        public MyWrappingSampler(Sampler inner)
        {
            this.Inner = inner;
            this.Description = $"Wrapping{{{inner.Description}}}";
        }

        public Sampler Inner { get; }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => this.Inner.ShouldSample(in samplingParameters);
    }

    private sealed class MyDisposableSampler : Sampler, IDisposable
    {
        public bool Disposed { get; private set; }

        public int ShutdownCalls { get; private set; }

        public int ShutdownTimeout { get; private set; }

        public bool ShutdownBeforeDispose { get; private set; }

        public int ShouldSampleCalls { get; private set; }

        public bool ShouldSampleAfterShutdown { get; private set; }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            this.ShouldSampleCalls++;
            this.ShouldSampleAfterShutdown |= this.ShutdownCalls > 0 || this.Disposed;

            return new(SamplingDecision.RecordAndSample);
        }

        public void Dispose()
        {
            this.ShutdownBeforeDispose = this.ShutdownCalls > 0;
            this.Disposed = true;
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            this.ShutdownCalls++;
            this.ShutdownTimeout = timeoutMilliseconds;
            return true;
        }
    }

    private sealed class ShutdownCallbackProcessor : BaseProcessor<Activity>
    {
        private readonly Action callback;

        public ShutdownCallbackProcessor(Action callback)
        {
            this.callback = callback;
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            this.callback();
            return base.OnShutdown(timeoutMilliseconds);
        }
    }

    private sealed class MyInstrumentation : IDisposable
    {
        internal TracerProvider? Provider;
        internal bool Disposed;

        public void Dispose() => this.Disposed = true;
    }

    private sealed class MyProcessor : BaseProcessor<Activity>
    {
        public string? Name;
        public bool Disposed;

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;

            base.Dispose(disposing);
        }
    }

    private sealed class MyTracerProviderBuilder : TracerProviderBuilder
    {
        public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
            => throw new NotImplementedException();

        public override TracerProviderBuilder AddLegacySource(string operationName)
            => throw new NotImplementedException();

        public override TracerProviderBuilder AddSource(params string[] names)
            => throw new NotImplementedException();
    }
}
