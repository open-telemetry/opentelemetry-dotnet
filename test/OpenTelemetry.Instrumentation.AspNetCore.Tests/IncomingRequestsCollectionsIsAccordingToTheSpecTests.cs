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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    public class IncomingRequestsCollectionsIsAccordingToTheSpecTests
        : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        public IncomingRequestsCollectionsIsAccordingToTheSpecTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Theory]
        [InlineData("/api/values", null, "user-agent", 503, "503")]
        [InlineData("/api/values", "?query=1", null, 503, null)]
        [InlineData("/api/exception", null, null, 503, null)]
        [InlineData("/api/exception", null, null, 503, null, true)]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan(
            string urlPath,
            string query,
            string userAgent,
            int statusCode,
            string reasonPhrase,
            bool recordException = false)
        {
            var exportedItems = new List<Activity>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices((IServiceCollection services) =>
                    {
                        services.AddSingleton<CallbackMiddleware.CallbackMiddlewareImpl>(new TestCallbackMiddlewareImpl(statusCode, reasonPhrase));
                        services.AddOpenTelemetryTracing((builder) => builder.AddAspNetCoreInstrumentation(options => options.RecordException = recordException)
                        .AddInMemoryExporter(exportedItems));
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
                    var path = urlPath;
                    if (query != null)
                    {
                        path += query;
                    }

                    var response = await client.GetAsync(path);
                }
                catch (Exception)
                {
                    // ignore errors
                }

                for (var i = 0; i < 10; i++)
                {
                    if (exportedItems.Count == 1)
                    {
                        break;
                    }

                    // We need to let End callback execute as it is executed AFTER response was returned.
                    // In unit tests environment there may be a lot of parallel unit tests executed, so
                    // giving some breezing room for the End callback to complete
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeHttpHost));
            Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
            Assert.Equal(urlPath, activity.GetTagValue(SemanticConventions.AttributeHttpTarget));
            Assert.Equal($"http://localhost{urlPath}{query}", activity.GetTagValue(SemanticConventions.AttributeHttpUrl));
            Assert.Equal(statusCode, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));

            if (statusCode == 503)
            {
                Assert.Equal(Status.Error.StatusCode, activity.GetStatus().StatusCode);
            }
            else
            {
                Assert.Equal(Status.Unset, activity.GetStatus());
            }

            // Instrumentation is not expected to set status description
            // as the reason can be inferred from SemanticConventions.AttributeHttpStatusCode
            if (!urlPath.EndsWith("exception"))
            {
                Assert.True(string.IsNullOrEmpty(activity.GetStatus().Description));
            }
            else
            {
                Assert.Equal("exception description", activity.GetStatus().Description);
            }

            if (recordException)
            {
                Assert.Single(activity.Events);
                Assert.Equal("exception", activity.Events.First().Name);
            }

            ValidateTagValue(activity, SemanticConventions.AttributeHttpUserAgent, userAgent);

            activity.Dispose();
        }

        private static void ValidateTagValue(Activity activity, string attribute, string expectedValue)
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

                if (context.Request.Path.Value.EndsWith("exception"))
                {
                    throw new Exception("exception description");
                }

                return false;
            }
        }
    }
}
