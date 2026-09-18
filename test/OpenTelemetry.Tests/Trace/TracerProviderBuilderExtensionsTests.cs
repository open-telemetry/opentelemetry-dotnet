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

    [Fact]
    public void ConfigureSamplerReceivesSamplerSetProgrammatically()
    {
        var resolvedSampler = new MySampler();

        Sampler? receivedSampler = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(resolvedSampler)
            .ConfigureSampler((sp, sampler) =>
            {
                receivedSampler = sampler;
                return sampler;
            })
            .Build();

        Assert.Same(resolvedSampler, receivedSampler);
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
    public void ConfigureSamplerReceivesResolvedSamplerWhenRegisteredBeforeSetSampler()
    {
        var resolvedSampler = new MySampler();

        Sampler? receivedSampler = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureSampler((sp, sampler) =>
            {
                receivedSampler = sampler;
                return sampler;
            })
            .SetSampler(resolvedSampler)
            .Build();

        Assert.Same(resolvedSampler, receivedSampler);
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

        // Note: The SDK only disposes the sampler it ends up holding. Disposing
        // a sampler which was replaced is the responsibility of the callback.
        Assert.False(resolvedSampler.Disposed);
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
        Assert.Single(provider.Instrumentations);
        Assert.True(provider.Instrumentations[0] is MyInstrumentation);
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

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(SamplingDecision.RecordAndSample);

        public void Dispose() => this.Disposed = true;
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
