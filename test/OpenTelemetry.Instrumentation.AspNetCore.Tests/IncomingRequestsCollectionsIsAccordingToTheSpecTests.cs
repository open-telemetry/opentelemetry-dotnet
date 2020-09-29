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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Trace;
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

        [Theory]
        [InlineData("/api/values", "user-agent", 503, "503")]
        [InlineData("/api/values", null, 503, null)]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan(
            string urlPath,
            string userAgent,
            int statusCode,
            string reasonPhrase)
        {
            var processor = new Mock<BaseProcessor<Activity>>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices((IServiceCollection services) =>
                    {
                        services.AddSingleton<CallbackMiddleware.CallbackMiddlewareImpl>(new TestCallbackMiddlewareImpl(statusCode, reasonPhrase));
                        services.AddOpenTelemetryTracing((builder) => builder.AddAspNetCoreInstrumentation()
                        .AddProcessor(processor.Object));
                    }))
                .CreateClient())
            {
                try
                {
                    if (!string.IsNullOrEmpty(userAgent))
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                    }

                    // Act
                    var response = await client.GetAsync(urlPath);
                }
                catch (Exception)
                {
                    // ignore errors
                }

                for (var i = 0; i < 10; i++)
                {
                    if (processor.Invocations.Count == 2)
                    {
                        break;
                    }

                    // We need to let End callback execute as it is executed AFTER response was returned.
                    // In unit tests environment there may be a lot of parallel unit tests executed, so
                    // giving some breezing room for the End callback to complete
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            Assert.Equal(2, processor.Invocations.Count); // begin and end was called
            var activity = (Activity)processor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeHttpHost));
            Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
            Assert.Equal(urlPath, activity.GetTagValue(SpanAttributeConstants.HttpPathKey));
            Assert.Equal($"http://localhost{urlPath}", activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
            Assert.Equal(statusCode, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));

            Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode(statusCode);
            Assert.Equal((int)status.StatusCode, activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));
            this.ValidateTagValue(activity, SpanAttributeConstants.StatusDescriptionKey, reasonPhrase);
            this.ValidateTagValue(activity, SemanticConventions.AttributeHttpUserAgent, userAgent);
        }

        private void ValidateTagValue(Activity activity, string attribute, string expectedValue)
        {
            if (string.IsNullOrEmpty(expectedValue))
            {
                Assert.Null(activity.GetTagValue(attribute));
            }
            else
            {
                Assert.Equal(expectedValue, activity.GetTagValue(attribute));
            }
        }

        public class TestCallbackMiddlewareImpl : CallbackMiddleware.CallbackMiddlewareImpl
        {
            private readonly int statusCode;
            private readonly string reasonPhrase;

            public TestCallbackMiddlewareImpl(int statusCode, string reasonPhrase)
            {
                this.statusCode = statusCode;
                this.reasonPhrase = reasonPhrase;
            }

            public override async Task<bool> ProcessAsync(HttpContext context)
            {
                context.Response.StatusCode = this.statusCode;
                context.Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = this.reasonPhrase;
                await context.Response.WriteAsync("empty");
                return false;
            }
        }
    }
}
