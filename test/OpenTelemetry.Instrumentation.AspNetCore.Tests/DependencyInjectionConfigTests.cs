// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public class DependencyInjectionConfigTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public DependencyInjectionConfigTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CustomName")]
    public void TestTracingOptionsDIConfig(string name)
    {
        name ??= Options.DefaultName;

        bool optionsPickedFromDI = false;
        void ConfigureTestServices(IServiceCollection services)
        {
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation(name, configureAspNetCoreTraceInstrumentationOptions: null));

            services.Configure<AspNetCoreTraceInstrumentationOptions>(name, options =>
            {
                optionsPickedFromDI = true;
            });
        }

        // Arrange
        using (var client = this.factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(ConfigureTestServices))
            .CreateClient())
        {
        }

        Assert.True(optionsPickedFromDI);
    }
}
