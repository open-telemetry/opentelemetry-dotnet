// <copyright file="MeterProviderBuilderExtensionsTests.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Extensions.DependencyInjection.Tests;

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
