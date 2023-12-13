// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Api.ProviderBuilderExtensions.Tests;

public class MeterProviderBuilderExtensionsTests
{
    [Fact]
    public void AddInstrumentationFromServiceProviderTest()
    {
        using var builder = new TestMeterProviderBuilder();

        builder.AddInstrumentation<TestInstrumentation>();

        var serviceProvider = builder.BuildServiceProvider();

        var instrumentation = serviceProvider.GetRequiredService<TestInstrumentation>();

        Assert.NotNull(instrumentation);

        var registrationCount = builder.InvokeRegistrations();

        Assert.Equal(1, registrationCount);
        Assert.Single(builder.Instrumentation);
        Assert.Equal(instrumentation, builder.Instrumentation[0]);
    }

    [Fact]
    public void AddInstrumentationUsingInstanceTest()
    {
        using var builder = new TestMeterProviderBuilder();

        var instrumentation = new TestInstrumentation();

        builder.AddInstrumentation(instrumentation);

        var serviceProvider = builder.BuildServiceProvider();
        var registrationCount = builder.InvokeRegistrations();

        Assert.Equal(1, registrationCount);
        Assert.Single(builder.Instrumentation);
        Assert.Equal(instrumentation, builder.Instrumentation[0]);
    }

    [Fact]
    public void AddInstrumentationUsingFactoryTest()
    {
        using var builder = new TestMeterProviderBuilder();

        var instrumentation = new TestInstrumentation();

        builder.AddInstrumentation(sp =>
        {
            Assert.NotNull(sp);

            return instrumentation;
        });

        var serviceProvider = builder.BuildServiceProvider();
        var registrationCount = builder.InvokeRegistrations();

        Assert.Equal(1, registrationCount);
        Assert.Single(builder.Instrumentation);
        Assert.Equal(instrumentation, builder.Instrumentation[0]);
    }

    [Fact]
    public void AddInstrumentationUsingFactoryAndProviderTest()
    {
        using var builder = new TestMeterProviderBuilder();

        var instrumentation = new TestInstrumentation();

        builder.AddInstrumentation((sp, provider) =>
        {
            Assert.NotNull(sp);
            Assert.NotNull(provider);

            return instrumentation;
        });

        var serviceProvider = builder.BuildServiceProvider();
        var registrationCount = builder.InvokeRegistrations();

        Assert.Equal(1, registrationCount);
        Assert.Single(builder.Instrumentation);
        Assert.Equal(instrumentation, builder.Instrumentation[0]);
    }

    [Fact]
    public void ConfigureServicesTest()
    {
        using var builder = new TestMeterProviderBuilder();

        builder.ConfigureServices(services => services.TryAddSingleton<TestInstrumentation>());

        var serviceProvider = builder.BuildServiceProvider();

        serviceProvider.GetRequiredService<TestInstrumentation>();
    }

    [Fact]
    public void ConfigureBuilderTest()
    {
        using var builder = new TestMeterProviderBuilder();

        builder.ConfigureBuilder((sp, builder) =>
        {
            Assert.NotNull(sp);
            Assert.NotNull(builder);

            builder.AddMeter("HelloWorld");
        });

        var serviceProvider = builder.BuildServiceProvider();
        var registrationCount = builder.InvokeRegistrations();

        Assert.Equal(1, registrationCount);
        Assert.Single(builder.Meters);
        Assert.Equal("HelloWorld", builder.Meters[0]);
    }

    private sealed class TestInstrumentation
    {
    }
}
