// <copyright file="TracerProviderBuilderBaseTests.cs" company="OpenTelemetry Authors">
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
