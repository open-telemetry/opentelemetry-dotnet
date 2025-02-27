// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Api.ProviderBuilderExtensions.Tests;

public class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ConfigureOpenTelemetryTracerProvider(int numberOfCalls)
    {
        var beforeServiceProviderInvocations = 0;
        var afterServiceProviderInvocations = 0;

        var services = new ServiceCollection();

        for (int i = 0; i < numberOfCalls; i++)
        {
            services.ConfigureOpenTelemetryTracerProvider(builder => beforeServiceProviderInvocations++);
            services.ConfigureOpenTelemetryTracerProvider((sp, builder) => afterServiceProviderInvocations++);
        }

        using var serviceProvider = services.BuildServiceProvider();

        var registrations = serviceProvider.GetServices<IConfigureTracerProviderBuilder>().ToArray();

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(0, afterServiceProviderInvocations);

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, null!);
        }

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(numberOfCalls, afterServiceProviderInvocations);

        Assert.Equal(numberOfCalls * 2, registrations.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ConfigureOpenTelemetryMeterProvider(int numberOfCalls)
    {
        var beforeServiceProviderInvocations = 0;
        var afterServiceProviderInvocations = 0;

        var services = new ServiceCollection();

        for (int i = 0; i < numberOfCalls; i++)
        {
            services.ConfigureOpenTelemetryMeterProvider(builder => beforeServiceProviderInvocations++);
            services.ConfigureOpenTelemetryMeterProvider((sp, builder) => afterServiceProviderInvocations++);
        }

        using var serviceProvider = services.BuildServiceProvider();

        var registrations = serviceProvider.GetServices<IConfigureMeterProviderBuilder>().ToArray();

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(0, afterServiceProviderInvocations);

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, null!);
        }

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(numberOfCalls, afterServiceProviderInvocations);

        Assert.Equal(numberOfCalls * 2, registrations.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ConfigureOpenTelemetryLoggerProvider(int numberOfCalls)
    {
        var beforeServiceProviderInvocations = 0;
        var afterServiceProviderInvocations = 0;

        var services = new ServiceCollection();

        for (int i = 0; i < numberOfCalls; i++)
        {
            services.ConfigureOpenTelemetryLoggerProvider(builder => beforeServiceProviderInvocations++);
            services.ConfigureOpenTelemetryLoggerProvider((sp, builder) => afterServiceProviderInvocations++);
        }

        using var serviceProvider = services.BuildServiceProvider();

        var registrations = serviceProvider.GetServices<IConfigureLoggerProviderBuilder>().ToArray();

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(0, afterServiceProviderInvocations);

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, null!);
        }

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(numberOfCalls, afterServiceProviderInvocations);

        Assert.Equal(numberOfCalls * 2, registrations.Length);
    }
}
