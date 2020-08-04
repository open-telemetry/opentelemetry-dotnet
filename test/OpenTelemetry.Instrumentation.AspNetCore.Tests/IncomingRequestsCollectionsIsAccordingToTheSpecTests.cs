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
            var spanProcessor = new Mock<ActivityProcessor>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices((IServiceCollection services) =>
                    {
                        services.AddSingleton<CallbackMiddleware.CallbackMiddlewareImpl>(new TestCallbackMiddlewareImpl(statusCode, reasonPhrase));
                        services.AddOpenTelemetry((builder) => builder.AddAspNetCoreInstrumentation()
                        .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object)));
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
            Assert.Equal("localhost:", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpHost).Value);
            Assert.Equal("GET", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpMethod).Value);
            Assert.Equal(urlPath, span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
            Assert.Equal($"http://localhost{urlPath}", span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpUrl).Value);
            Assert.Equal(statusCode, span.TagObjects.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpStatusCode).Value);

            Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode(statusCode);
            Assert.Equal(SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode), span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
            this.ValidateTagValue(span, SpanAttributeConstants.StatusDescriptionKey, reasonPhrase);
            this.ValidateTagValue(span, SemanticConventions.AttributeHttpUserAgent, userAgent);
        }

        private void ValidateTagValue(Activity activity, string attribute, string expectedValue)
        {
            if (string.IsNullOrEmpty(expectedValue))
            {
                Assert.True(activity.Tags.All(i => i.Key != attribute));
            }
            else
            {
                Assert.Equal(expectedValue, activity.Tags.FirstOrDefault(i => i.Key == attribute).Value);
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
