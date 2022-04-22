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
using OpenTelemetry.Trace;
#if NETCOREAPP3_1
using TestApp.AspNetCore._3._1;
#endif
#if NET6_0
using TestApp.AspNetCore._6._0;
#endif
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public class DependencyInjectionConfigTests
        : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        public DependencyInjectionConfigTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public void TestDIConfig()
        {
            bool optionsPickedFromDI = false;
            void ConfigureTestServices(IServiceCollection services)
            {
                services.AddOpenTelemetryTracing(
                    builder =>
                     {
                         builder.AddAspNetCoreInstrumentation();
                     });

                services.Configure<AspNetCoreInstrumentationOptions>(options =>
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
