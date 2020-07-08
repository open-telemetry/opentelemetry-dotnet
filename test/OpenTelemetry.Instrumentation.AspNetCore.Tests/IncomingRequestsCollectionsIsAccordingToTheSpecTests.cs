// <copyright file="IncomingRequestsCollectionsIsAccordingToTheSpecTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
#if NETCOREAPP2_1
using TestApp.AspNetCore._2._1;
#else
using TestApp.AspNetCore._3._1;
#endif
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public class IncomingRequestsCollectionsIsAccordingToTheSpecTests
        : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        public IncomingRequestsCollectionsIsAccordingToTheSpecTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan()
        {
            var spanProcessor = new Mock<ActivityProcessor>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices((IServiceCollection services) =>
                    {
                        services.AddSingleton<CallbackMiddleware.CallbackMiddlewareImpl>(new TestCallbackMiddlewareImpl());
                        services.AddOpenTelemetrySdk((builder) => builder.AddRequestInstrumentation()
                        .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object)));
                    }))
                .CreateClient())
            {
                try
                {
                    // Act
                    var response = await client.GetAsync("/api/values");
                }
                catch (Exception)
                {
                    // ignore errors
                }

                for (var i = 0; i < 10; i++)
                {
                    if (spanProcessor.Invocations.Count == 2)
                    {
                        break;
                    }

                    // We need to let End callback execute as it is executed AFTER response was returned.
                    // In unit tests environment there may be a lot of parallel unit tests executed, so
                    // giving some breezing room for the End callback to complete
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.Equal("/api/values", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
            Assert.Equal("503", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpStatusCodeKey).Value);
        }

        public class TestCallbackMiddlewareImpl : CallbackMiddleware.CallbackMiddlewareImpl
        {
            public override async Task<bool> ProcessAsync(HttpContext context)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("empty");
                return false;
            }
        }
    }
}
