// <copyright file="ProviderBuilderExtensionsTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class ProviderBuilderExtensionsTests
{
    [Fact]
    public void TraceProvider_AddHttpClientInstrumentation_NamedOptions()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HttpClientInstrumentationOptions>(_ =>
                    defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<HttpClientInstrumentationOptions>(
                    "Instrumentation2",
                    _ => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddHttpClientInstrumentation()
            .AddHttpClientInstrumentation("Instrumentation2", configureHttpClientInstrumentationOptions: null)
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CustomName")]
    public void AddHttpClientInstrumentationUsesOptionsApi(string name)
    {
        name ??= Options.DefaultName;

        int configurationDelegateInvocations = 0;

        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HttpClientInstrumentationOptions>(name, _ => configurationDelegateInvocations++);
            })
            .AddProcessor(activityProcessor.Object)
            .AddHttpClientInstrumentation(
                name,
                options => Assert.IsType<HttpClientInstrumentationOptions>(options))
            .Build();

        Assert.Equal(1, configurationDelegateInvocations);
    }

    [Fact]
    public void TraceProvider_AddHttpClientInstrumentation_NullBuilder()
    {
        TracerProviderBuilder builder = null;
        Assert.Throws<ArgumentNullException>(() => builder.AddHttpClientInstrumentation());
    }

    [Fact]
    public void MeterProvider_AddHttpClientInstrumentation_NullBuilder()
    {
        MeterProviderBuilder builder = null;
        Assert.Throws<ArgumentNullException>(() => builder.AddHttpClientInstrumentation());
    }
}
