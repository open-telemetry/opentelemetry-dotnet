// <copyright file="DependencyInjectionConfigTests.cs" company="OpenTelemetry Authors">
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

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
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
                services.AddOpenTelemetryTracing(
                    builder => builder.AddAspNetCoreInstrumentation(name, configureAspNetCoreInstrumentationOptions: null));

                services.Configure<AspNetCoreInstrumentationOptions>(name, options =>
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

        [Theory]
        [InlineData(null)]
        [InlineData("CustomName")]
        public void TestMetricsOptionsDIConfig(string name)
        {
            name ??= Options.DefaultName;

            bool optionsPickedFromDI = false;
            void ConfigureTestServices(IServiceCollection services)
            {
                services.AddOpenTelemetryMetrics(
                    builder => builder.AddAspNetCoreInstrumentation(name, configureAspNetCoreInstrumentationOptions: null));

                services.Configure<AspNetCoreMetricsInstrumentationOptions>(name, options =>
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
}
