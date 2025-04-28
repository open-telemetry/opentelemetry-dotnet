// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MeterProviderBuilderExtensionsTests
{
    [Fact]
    public void ServiceLifecycleAvailableToSDKBuilderTest()
    {
        var builder = Sdk.CreateMeterProviderBuilder();

        MyInstrumentation? myInstrumentation = null;

        RunBuilderServiceLifecycleTest(
            builder,
            () =>
            {
                var provider = builder.Build() as MeterProviderSdk;

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
    public void AddReaderUsingDependencyInjectionTest()
    {
        var builder = Sdk.CreateMeterProviderBuilder();

        builder.AddReader<MyReader>();
        builder.AddReader<MyReader>();

        using var provider = builder.Build() as MeterProviderSdk;

        Assert.NotNull(provider);
        Assert.NotNull(provider.OwnedServiceProvider);

        var readers = ((IServiceProvider)provider.OwnedServiceProvider).GetServices<MyReader>();

        // Note: Two "Add" calls but it is a singleton so only a single registration is produced
        Assert.Single(readers);

        var reader = provider.Reader as CompositeMetricReader;

        Assert.NotNull(reader);

        // Note: Two "Add" calls due yield two readers added to provider, even though they are the same
        Assert.True(reader.Head.Value is MyReader);
        Assert.True(reader.Head.Next?.Value is MyReader);
    }

    [Fact]
    public void AddInstrumentationTest()
    {
        List<object>? instrumentation = null;

        using (var provider = Sdk.CreateMeterProviderBuilder()
            .AddInstrumentation<MyInstrumentation>()
            .AddInstrumentation((sp, provider) => new MyInstrumentation() { Provider = provider })
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddInstrumentation(new MyInstrumentation())
#pragma warning restore CA2000 // Dispose objects before losing scope
            .AddInstrumentation(() => (object?)null)
            .Build() as MeterProviderSdk)
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
        var builder = Sdk.CreateMeterProviderBuilder();

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

        using var provider = builder.Build() as MeterProviderSdk;

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

        bool configureBuilderCalled = false;

        using var provider = Sdk.CreateMeterProviderBuilder()
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
        bool configureBuilderCalled = false;

        using var provider = Sdk.CreateMeterProviderBuilder()
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
    public void MeterProviderNestedResolutionUsingBuilderTest(bool callNestedConfigure)
    {
        bool innerConfigureBuilderTestExecuted = false;
        bool innerConfigureOpenTelemetryLoggerProviderTestExecuted = false;
        bool innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted = false;

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                if (callNestedConfigure)
                {
                    services.ConfigureOpenTelemetryMeterProvider(
                        builder =>
                        {
                            innerConfigureOpenTelemetryLoggerProviderTestExecuted = true;
                            builder.AddInstrumentation<MyInstrumentation>();
                        });
                    services.ConfigureOpenTelemetryMeterProvider(
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
                Assert.Throws<NotSupportedException>(() => sp.GetService<MeterProvider>());
            })
            .Build() as MeterProviderSdk;

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

        Assert.Throws<NotSupportedException>(() => provider.GetServiceProvider()?.GetService<MeterProvider>());
    }

    [Fact]
    public void MeterProviderAddReaderFactoryTest()
    {
        bool factoryInvoked = false;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddReader(sp =>
            {
                factoryInvoked = true;

                Assert.NotNull(sp);

                return new MyReader();
            })
            .Build() as MeterProviderSdk;

        Assert.True(factoryInvoked);

        Assert.NotNull(meterProvider);
        Assert.True(meterProvider.Reader is MyReader);
    }

    [Fact]
    public void MeterProviderBuilderCustomImplementationBuildTest()
    {
        var builder = new MyMeterProviderBuilder();

        Assert.Throws<NotSupportedException>(() => builder.Build());
    }

    private static void RunBuilderServiceLifecycleTest(
        MeterProviderBuilder builder,
        Func<MeterProviderSdk> buildFunc,
        Action<MeterProviderSdk> postAction)
    {
        var baseBuilder = builder as MeterProviderBuilderBase;

        builder.AddMeter("TestSource");

        bool configureServicesCalled = false;
        builder.ConfigureServices(services =>
        {
            configureServicesCalled = true;

            Assert.NotNull(services);

            services.TryAddSingleton<MyInstrumentation>();
            services.TryAddSingleton<MyReader>();

            // Note: This is strange to call ConfigureOpenTelemetryMeterProvider here, but supported
            services.ConfigureOpenTelemetryMeterProvider((sp, b) =>
            {
                Assert.Throws<NotSupportedException>(() => b.ConfigureServices(services => { }));

                b.AddInstrumentation(sp.GetRequiredService<MyInstrumentation>());
            });
        });

        int configureBuilderInvocations = 0;
        builder.ConfigureBuilder((sp, builder) =>
        {
            configureBuilderInvocations++;

            var sdkBuilder = builder as MeterProviderBuilderSdk;
            Assert.NotNull(sdkBuilder);

            builder.AddMeter("TestSource2");

            Assert.Contains(sdkBuilder.MeterSources, s => s == "TestSource");
            Assert.Contains(sdkBuilder.MeterSources, s => s == "TestSource2");

            // Note: Services can't be configured at this stage
            Assert.Throws<NotSupportedException>(
                () => builder.ConfigureServices(services => services.TryAddSingleton<MeterProviderBuilderExtensionsTests>()));

            builder.AddReader(sp.GetRequiredService<MyReader>());

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

        Assert.Single(provider.Instrumentations);
        Assert.True(provider.Instrumentations[0] is MyInstrumentation);
        Assert.True(provider.Reader is MyReader);

        postAction(provider);
    }

    private sealed class MyInstrumentation : IDisposable
    {
        internal MeterProvider? Provider;
        internal bool Disposed;

        public void Dispose()
        {
            this.Disposed = true;
        }
    }

    private sealed class MyReader : MetricReader
    {
    }

    private sealed class MyMeterProviderBuilder : MeterProviderBuilder
    {
        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            throw new NotImplementedException();
        }

        public override MeterProviderBuilder AddMeter(params string[] names)
        {
            throw new NotImplementedException();
        }
    }
}
