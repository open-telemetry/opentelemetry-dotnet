// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TracerProviderBuilderBaseTests
{
    [Fact]
    public void AddInstrumentationInvokesFactoryTest()
    {
        bool factoryInvoked = false;

        var instrumentation = new TestTracerProviderBuilder();
        instrumentation.AddInstrumentationViaProtectedMethod(() =>
        {
            factoryInvoked = true;

            return null;
        });

        using var provider = instrumentation.Build();

        Assert.True(factoryInvoked);
    }

    [Fact]
    public void AddInstrumentationValidatesInputTest()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new TestTracerProviderBuilder().AddInstrumentationViaProtectedMethod(
                name: null,
                version: "1.0.0",
                factory: () => null);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            new TestTracerProviderBuilder().AddInstrumentationViaProtectedMethod(
                name: "name",
                version: null,
                factory: () => null);
        });

        Assert.Throws<ArgumentNullException>(() =>
        {
            new TestTracerProviderBuilder().AddInstrumentationViaProtectedMethod(
                name: "name",
                version: "1.0.0",
                factory: null);
        });
    }

    private sealed class TestTracerProviderBuilder : TracerProviderBuilderBase
    {
        public void AddInstrumentationViaProtectedMethod(Func<object?> factory)
        {
            this.AddInstrumentation("MyName", "MyVersion", factory);
        }

        public void AddInstrumentationViaProtectedMethod(string? name, string? version, Func<object?>? factory)
        {
            this.AddInstrumentation(name!, version!, factory!);
        }
    }
}
