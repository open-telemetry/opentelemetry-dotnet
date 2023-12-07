// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Api.ProviderBuilderExtensions.Tests;

public class LoggerProviderBuilderExtensionsTests
{
    [Fact]
    public void AddInstrumentationFromServiceProviderTest()
    {
        using var builder = new TestLoggerProviderBuilder();

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
        using var builder = new TestLoggerProviderBuilder();

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
        using var builder = new TestLoggerProviderBuilder();

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
        using var builder = new TestLoggerProviderBuilder();

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
        using var builder = new TestLoggerProviderBuilder();

        builder.ConfigureServices(services => services.TryAddSingleton<TestInstrumentation>());

        var serviceProvider = builder.BuildServiceProvider();

        serviceProvider.GetRequiredService<TestInstrumentation>();
    }

    [Fact]
    public void ConfigureBuilderTest()
    {
        var instrumentation = new object();

        using var builder = new TestLoggerProviderBuilder();

        builder.ConfigureBuilder((sp, builder) =>
        {
            Assert.NotNull(sp);
            Assert.NotNull(builder);

            builder.AddInstrumentation(instrumentation);
        });

        var serviceProvider = builder.BuildServiceProvider();
        var registrationCount = builder.InvokeRegistrations();

        Assert.Equal(1, registrationCount);
        Assert.Single(builder.Instrumentation);
        Assert.True(ReferenceEquals(instrumentation, builder.Instrumentation[0]));
    }

    private sealed class TestInstrumentation
    {
    }
}