// <copyright file="ServiceCollectionExtensionsTests.cs" company="OpenTelemetry Authors">
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

        var registrations = serviceProvider.GetServices<IConfigureTracerProviderBuilder>();

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(0, afterServiceProviderInvocations);

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, null!);
        }

        Assert.Equal(numberOfCalls, beforeServiceProviderInvocations);
        Assert.Equal(numberOfCalls, afterServiceProviderInvocations);

        Assert.Equal(numberOfCalls * 2, registrations.Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ConfigureOpenTelemetryMeterProvider(int numberOfCalls)
    {
        var invocations = 0;

        var services = new ServiceCollection();

        for (int i = 0; i < numberOfCalls; i++)
        {
            services.ConfigureOpenTelemetryMeterProvider((sp, builder) => invocations++);
        }

        using var serviceProvider = services.BuildServiceProvider();

        var registrations = serviceProvider.GetServices<IConfigureMeterProviderBuilder>();

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, null!);
        }

        Assert.Equal(invocations, registrations.Count());
        Assert.Equal(numberOfCalls, registrations.Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ConfigureOpenTelemetryLoggerProvider(int numberOfCalls)
    {
        var invocations = 0;

        var services = new ServiceCollection();

        for (int i = 0; i < numberOfCalls; i++)
        {
            services.ConfigureOpenTelemetryLoggerProvider((sp, builder) => invocations++);
        }

        using var serviceProvider = services.BuildServiceProvider();

        var registrations = serviceProvider.GetServices<IConfigureLoggerProviderBuilder>();

        foreach (var registration in registrations)
        {
            registration.ConfigureBuilder(serviceProvider, null!);
        }

        Assert.Equal(invocations, registrations.Count());
        Assert.Equal(numberOfCalls, registrations.Count());
    }

    private sealed class CustomType
    {
    }
}
