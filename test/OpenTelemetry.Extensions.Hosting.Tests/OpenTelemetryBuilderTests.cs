// <copyright file="OpenTelemetryBuilderTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class OpenTelemetryBuilderTests
{
    [Fact]
    public void ConfigureResourceTest()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.Clear().AddResource(
                new Resource(new Dictionary<string, object> { ["key1"] = "value1" })))
            .WithLogging(logging => logging.ConfigureResource(r => r.AddResource(
                new Resource(new Dictionary<string, object> { ["l_key1"] = "l_value1" }))))
            .WithMetrics(metrics => metrics.ConfigureResource(r => r.AddResource(
                new Resource(new Dictionary<string, object> { ["m_key1"] = "m_value1" }))))
            .WithTracing(tracing => tracing.ConfigureResource(r => r.AddResource(
                new Resource(new Dictionary<string, object> { ["t_key1"] = "t_value1" }))));

        using var sp = services.BuildServiceProvider();

        var tracerProvider = sp.GetRequiredService<TracerProvider>() as TracerProviderSdk;
        var meterProvider = sp.GetRequiredService<MeterProvider>() as MeterProviderSdk;
        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);
        Assert.NotNull(loggerProvider);

        Assert.Equal(2, tracerProvider.Resource.Attributes.Count());
        Assert.Contains(
            tracerProvider.Resource.Attributes,
            kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(
            tracerProvider.Resource.Attributes,
            kvp => kvp.Key == "t_key1" && (string)kvp.Value == "t_value1");

        Assert.Equal(2, meterProvider.Resource.Attributes.Count());
        Assert.Contains(
            meterProvider.Resource.Attributes,
            kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(
            meterProvider.Resource.Attributes,
            kvp => kvp.Key == "m_key1" && (string)kvp.Value == "m_value1");

        Assert.Equal(2, loggerProvider.Resource.Attributes.Count());
        Assert.Contains(
            loggerProvider.Resource.Attributes,
            kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(
            loggerProvider.Resource.Attributes,
            kvp => kvp.Key == "l_key1" && (string)kvp.Value == "l_value1");
    }
}
