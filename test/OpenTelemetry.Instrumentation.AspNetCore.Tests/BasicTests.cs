// <copyright file="BasicTests.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
#if NETCOREAPP2_1
using TestApp.AspNetCore._2._1;
#else
using TestApp.AspNetCore._3._1;
#endif
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    // See https://github.com/aspnet/Docs/tree/master/aspnetcore/test/integration-tests/samples/2.x/IntegrationTestsSample
    public class BasicTests
        : IClassFixture<WebApplicationFactory<Startup>>, IDisposable
    {
        private readonly WebApplicationFactory<Startup> factory;
        private TracerProvider openTelemetrySdk = null;

        public BasicTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public void AddAspNetCoreInstrumentation_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddAspNetCoreInstrumentation());
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var activityProcessor = new Mock<ActivityProcessor>();
            void ConfigureTestServices(IServiceCollection services)
            {
                this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .SetResource(expectedResource)
                    .AddProcessor(activityProcessor.Object)
                    .Build();
            }

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices))
                .CreateClient())
            {
                // Act
                var response = await client.GetAsync("/api/values");

                // Assert
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(activityProcessor, 2);
            }

            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            ValidateAspNetCoreActivity(activity, "/api/values", expectedResource);
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallUsesParentContext()
        {
            var activityProcessor = new Mock<ActivityProcessor>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder().AddAspNetCoreInstrumentation()
                        .AddProcessor(activityProcessor.Object)
                        .Build();
                    })))
            {
                using var client = testFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/values/2");
                request.Headers.Add("traceparent", $"00-{expectedTraceId}-{expectedSpanId}-01");

                // Act
                var response = await client.SendAsync(request);

                // Assert
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(activityProcessor, 2);
            }

            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("api/Values/{id}", activity.DisplayName);
            Assert.Equal("/api/values/2", activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);

            Assert.Equal(expectedTraceId, activity.Context.TraceId);
            Assert.Equal(expectedSpanId, activity.ParentSpanId);
        }

        [Fact]
        public async Task CustomPropagator()
        {
            var activityProcessor = new Mock<ActivityProcessor>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var propagator = new Mock<IPropagator>();
            propagator.Setup(m => m.Extract(It.IsAny<PropagationContext>(), It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>())).Returns(
                new PropagationContext(
                    new ActivityContext(
                        expectedTraceId,
                        expectedSpanId,
                        ActivityTraceFlags.Recorded),
                    default));

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                            .AddAspNetCoreInstrumentation((opt) => opt.Propagator = propagator.Object)
                            .AddProcessor(activityProcessor.Object)
                            .Build();
                    })))
            {
                using var client = testFactory.CreateClient();
                var response = await client.GetAsync("/api/values/2");
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(activityProcessor, 2);
            }

            // begin and end was called once each.
            Assert.Equal(2, activityProcessor.Invocations.Count);
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.True(activity.Duration != TimeSpan.Zero);
            Assert.Equal("api/Values/{id}", activity.DisplayName);
            Assert.Equal("/api/values/2", activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);

            Assert.Equal(expectedTraceId, activity.Context.TraceId);
            Assert.Equal(expectedSpanId, activity.ParentSpanId);
        }

        [Fact]
        public async Task RequestNotCollectedWhenFilterIsApplied()
        {
            var activityProcessor = new Mock<ActivityProcessor>();

            void ConfigureTestServices(IServiceCollection services)
            {
                this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation((opt) => opt.Filter = (ctx) => ctx.Request.Path != "/api/values/2")
                    .AddProcessor(activityProcessor.Object)
                    .Build();
            }

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices)))
            {
                using var client = testFactory.CreateClient();

                // Act
                var response1 = await client.GetAsync("/api/values");
                var response2 = await client.GetAsync("/api/values/2");

                // Assert
                response1.EnsureSuccessStatusCode(); // Status Code 200-299
                response2.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(activityProcessor, 2);
            }

            // we should only create one span and never call processor with another
            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("/api/values", activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
        }

        [Fact]
        public async Task RequestNotCollectedWhenFilterThrowException()
        {
            var activityProcessor = new Mock<ActivityProcessor>();

            void ConfigureTestServices(IServiceCollection services)
            {
                this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation((opt) => opt.Filter = (ctx) =>
                    {
                        if (ctx.Request.Path == "/api/values/2")
                        {
                            throw new Exception("from InstrumentationFilter");
                        }
                        else
                        {
                            return true;
                        }
                    })
                    .AddProcessor(activityProcessor.Object)
                    .Build();
            }

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices)))
            {
                using var client = testFactory.CreateClient();

                // Act
                using (var inMemoryEventListener = new InMemoryEventListener(AspNetCoreInstrumentationEventSource.Log))
                {
                    var response1 = await client.GetAsync("/api/values");
                    var response2 = await client.GetAsync("/api/values/2");

                    response1.EnsureSuccessStatusCode(); // Status Code 200-299
                    response2.EnsureSuccessStatusCode(); // Status Code 200-299
                    Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 3));
                }

                WaitForProcessorInvocations(activityProcessor, 2);
            }

            // As InstrumentationFilter threw, we continue as if the
            // InstrumentationFilter did not exist.
            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("/api/values", activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
        }

        public void Dispose()
        {
            this.openTelemetrySdk?.Dispose();
        }

        private static void WaitForProcessorInvocations(Mock<ActivityProcessor> activityProcessor, int invocationCount)
        {
            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so
            // giving some breezing room for the End callback to complete
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return activityProcessor.Invocations.Count >= invocationCount;
                },
                TimeSpan.FromSeconds(1)));
        }

        private static void ValidateAspNetCoreActivity(Activity activityToValidate, string expectedHttpPath, Resources.Resource expectedResource)
        {
            Assert.Equal(ActivityKind.Server, activityToValidate.Kind);
            Assert.Equal(expectedHttpPath, activityToValidate.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
            Assert.Equal(expectedResource, activityToValidate.GetResource());
            var request = activityToValidate.GetCustomProperty(HttpInListener.RequestCustomPropertyName);
            Assert.NotNull(request);
            Assert.True(request is HttpRequest);

            var response = activityToValidate.GetCustomProperty(HttpInListener.ResponseCustomPropertyName);
            Assert.NotNull(response);
            Assert.True(response is HttpResponse);
        }
    }
}
