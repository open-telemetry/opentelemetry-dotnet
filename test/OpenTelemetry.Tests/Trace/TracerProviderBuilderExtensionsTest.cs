// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TracerProviderBuilderExtensionsTest
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

        Activity activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new Exception("Oops!");
            }
        }
        catch (Exception)
        {
        }

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

        Activity activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new Exception("Oops!");
            }
        }
        catch (Exception)
        {
        }

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

        Activity activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new Exception("Oops!");
            }
        }
        catch (Exception)
        {
        }

        Assert.Equal(StatusCode.Unset, activity.GetStatus().StatusCode);
    }

    [Fact]
    public void ServiceLifecycleAvailableToSDKBuilderTest()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        MyInstrumentation myInstrumentation = null;

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
    public void AddProcessorUsingDependencyInjectionTest()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        builder.AddProcessor<MyProcessor>();
        builder.AddProcessor<MyProcessor>();

        using var provider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(provider);

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
        List<object> instrumentation = null;

        using (var provider = Sdk.CreateTracerProviderBuilder()
            .AddInstrumentation<MyInstrumentation>()
            .AddInstrumentation((sp, provider) => new MyInstrumentation() { Provider = provider })
            .AddInstrumentation(new MyInstrumentation())
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

            instrumentation = new List<object>(provider.Instrumentations);
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

        int configureInvocations = 0;
        bool serviceProviderTestExecuted = false;

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

        Assert.Single(provider.Resource.Attributes);
        Assert.Contains(provider.Resource.Attributes, kvp => kvp.Key == "key2" && (string)kvp.Value == "value2");
    }

    [Fact]
    public void ConfigureBuilderIConfigurationAvailableTest()
    {
        Environment.SetEnvironmentVariable("TEST_KEY", "TEST_KEY_VALUE");

        bool configureBuilderCalled = false;

        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureBuilder((sp, builder) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                configureBuilderCalled = true;

                var testKeyValue = configuration.GetValue<string>("TEST_KEY", null);

                Assert.Equal("TEST_KEY_VALUE", testKeyValue);
            })
            .Build();

        Assert.True(configureBuilderCalled);

        Environment.SetEnvironmentVariable("TEST_KEY", null);
    }

    [Fact]
    public void ConfigureBuilderIConfigurationModifiableTest()
    {
        bool configureBuilderCalled = false;

        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["TEST_KEY_2"] = "TEST_KEY_2_VALUE" })
                    .Build();

                services.AddSingleton<IConfiguration>(configuration);
            })
            .ConfigureBuilder((sp, builder) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();

                configureBuilderCalled = true;

                var testKey2Value = configuration.GetValue<string>("TEST_KEY_2", null);

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
        bool innerConfigureBuilderTestExecuted = false;
        bool innerConfigureOpenTelemetryLoggerProviderTestExecuted = false;
        bool innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted = false;

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
        bool factoryInvoked = false;

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

    [Fact]
    public void TracerProviderAddProcessorFactoryTest()
    {
        bool factoryInvoked = false;

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

        bool configureServicesCalled = false;
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

        int configureBuilderInvocations = 0;
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
                () => builder.ConfigureServices(services => services.TryAddSingleton<TracerProviderBuilderExtensionsTest>()));

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
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
    }

    private sealed class MyInstrumentation : IDisposable
    {
        internal TracerProvider Provider;
        internal bool Disposed;

        public void Dispose()
        {
            this.Disposed = true;
        }
    }

    private sealed class MyProcessor : BaseProcessor<Activity>
    {
    }

    private sealed class MyExporter : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            return ExportResult.Success;
        }
    }
}
