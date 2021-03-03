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
#elif NETCOREAPP3_1
using TestApp.AspNetCore._3._1;
#else
using TestApp.AspNetCore._5._0;
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
        public async Task StatusIsUnsetOn200Response()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            void ConfigureTestServices(IServiceCollection services)
            {
                this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
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

                WaitForProcessorInvocations(activityProcessor, 3);
            }

            Assert.Equal(3, activityProcessor.Invocations.Count); // begin and end was called

            // we should only call Processor.OnEnd for the "/api/values" request
            Assert.Single(activityProcessor.Invocations, invo => invo.Method.Name == "OnEnd");
            var activity = activityProcessor.Invocations.FirstOrDefault(invo => invo.Method.Name == "OnEnd").Arguments[0] as Activity;

            Assert.Equal(200, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));

            var status = activity.GetStatus();
            Assert.Equal(status, Status.Unset);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan(bool shouldEnrich)
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            void ConfigureTestServices(IServiceCollection services)
            {
                this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        if (shouldEnrich)
                        {
                            options.Enrich = ActivityEnrichment;
                        }
                    })
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

                WaitForProcessorInvocations(activityProcessor, 3);
            }

            Assert.Equal(3, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[2].Arguments[0];

            ValidateAspNetCoreActivity(activity, "/api/values");
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallUsesParentContext()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

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

                WaitForProcessorInvocations(activityProcessor, 3);
            }

            // List of invocations
            // 1. SetParentProvider for TracerProviderSdk
            // 2. OnStart for the activity created by AspNetCore with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            // 3. OnStart for the sibling activity created by the instrumentation library with the OperationName: ActivityCreatedByHttpInListener
            // 4. OnEnd for the sibling activity created by the instrumentation library with the OperationName: ActivityCreatedByHttpInListener

            // we should only call Processor.OnEnd once for the sibling activity with the OperationName ActivityCreatedByHttpInListener
            Assert.Single(activityProcessor.Invocations, invo => invo.Method.Name == "OnEnd");
            var activity = activityProcessor.Invocations.FirstOrDefault(invo => invo.Method.Name == "OnEnd").Arguments[0] as Activity;

#if !NETCOREAPP2_1
            // ASP.NET Core after 2.x is W3C aware and hence Activity created by it
            // must be used.
            Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", activity.OperationName);
#else
            // ASP.NET Core before 3.x is not W3C aware and hence Activity created by it
            // is always ignored and new one is created by the Instrumentation
            Assert.Equal("ActivityCreatedByHttpInListener", activity.OperationName);
#endif
            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("api/Values/{id}", activity.DisplayName);
            Assert.Equal("/api/values/2", activity.GetTagValue(SpanAttributeConstants.HttpPathKey) as string);

            Assert.Equal(expectedTraceId, activity.Context.TraceId);
            Assert.Equal(expectedSpanId, activity.ParentSpanId);
        }

        [Fact]
        public async Task CustomPropagator()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var propagator = new Mock<TextMapPropagator>();
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
                        Sdk.SetDefaultTextMapPropagator(propagator.Object);
                        this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                            .AddAspNetCoreInstrumentation()
                            .AddProcessor(activityProcessor.Object)
                            .Build();
                    })))
            {
                using var client = testFactory.CreateClient();
                var response = await client.GetAsync("/api/values/2");
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(activityProcessor, 4);
            }

            // List of invocations on the processor
            // 1. SetParentProvider for TracerProviderSdk
            // 2. OnStart for the activity created by AspNetCore with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            // 3. OnStart for the sibling activity created by the instrumentation library with the OperationName: ActivityCreatedByHttpInListener
            // 4. OnEnd for the sibling activity created by the instrumentation library with the OperationName: ActivityCreatedByHttpInListener
            Assert.Equal(4, activityProcessor.Invocations.Count);

            var startedActivities = activityProcessor.Invocations.Where(invo => invo.Method.Name == "OnStart");
            var stoppedActivities = activityProcessor.Invocations.Where(invo => invo.Method.Name == "OnEnd");
            Assert.Equal(2, startedActivities.Count());
            Assert.Single(stoppedActivities);

            // The activity created by the framework and the sibling activity are both sent to Processor.OnStart
            Assert.Contains(startedActivities, item =>
            {
                var startedActivity = item.Arguments[0] as Activity;
                return startedActivity.OperationName == HttpInListener.ActivityOperationName;
            });

            Assert.Contains(startedActivities, item =>
            {
                var startedActivity = item.Arguments[0] as Activity;
                return startedActivity.OperationName == HttpInListener.ActivityNameByHttpInListener;
            });

            // Only the sibling activity is sent to Processor.OnEnd
            Assert.Contains(stoppedActivities, item =>
            {
                var stoppedActivity = item.Arguments[0] as Activity;
                return stoppedActivity.OperationName == HttpInListener.ActivityNameByHttpInListener;
            });

            var activity = activityProcessor.Invocations.FirstOrDefault(invo => invo.Method.Name == "OnEnd").Arguments[0] as Activity;
            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.True(activity.Duration != TimeSpan.Zero);
            Assert.Equal("api/Values/{id}", activity.DisplayName);
            Assert.Equal("/api/values/2", activity.GetTagValue(SpanAttributeConstants.HttpPathKey) as string);

            Assert.Equal(expectedTraceId, activity.Context.TraceId);
            Assert.Equal(expectedSpanId, activity.ParentSpanId);
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator(),
            }));
        }

        [Fact]
        public async Task RequestNotCollectedWhenFilterIsApplied()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

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

                WaitForProcessorInvocations(activityProcessor, 4);
            }

            // 1. SetParentProvider for TracerProviderSdk
            // 2. OnStart for the activity created by AspNetCore for "/api/values" with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            // 3. OnEnd for the activity created by AspNetCore for "/api/values" with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            // 4. OnStart for the activity created by AspNetCore for "/api/values/2" with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            Assert.Equal(4, activityProcessor.Invocations.Count);

            // we should only call Processor.OnEnd for the "/api/values" request
            Assert.Single(activityProcessor.Invocations, invo => invo.Method.Name == "OnEnd");
            var activity = activityProcessor.Invocations.FirstOrDefault(invo => invo.Method.Name == "OnEnd").Arguments[0] as Activity;

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("/api/values", activity.GetTagValue(SpanAttributeConstants.HttpPathKey) as string);
        }

        [Fact]
        public async Task RequestNotCollectedWhenFilterThrowException()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

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

                WaitForProcessorInvocations(activityProcessor, 3);
            }

            // As InstrumentationFilter threw, we continue as if the
            // InstrumentationFilter did not exist.

            // List of invocations on the processor
            // 1. SetParentProvider for TracerProviderSdk
            // 2. OnStart for the activity created by AspNetCore for "/api/values" with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            // 3. OnEnd for the activity created by AspNetCore for "/api/values" with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn
            // 4. OnStart for the activity created by AspNetCore for "/api/values/2" with the OperationName: Microsoft.AspNetCore.Hosting.HttpRequestIn

            // we should only call Processor.OnEnd for the "/api/values" request
            Assert.Single(activityProcessor.Invocations, invo => invo.Method.Name == "OnEnd");
            var activity = activityProcessor.Invocations.FirstOrDefault(invo => invo.Method.Name == "OnEnd").Arguments[0] as Activity;

            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("/api/values", activity.GetTagValue(SpanAttributeConstants.HttpPathKey) as string);
        }

        public void Dispose()
        {
            this.openTelemetrySdk?.Dispose();
        }

        private static void WaitForProcessorInvocations(Mock<BaseProcessor<Activity>> activityProcessor, int invocationCount)
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

        private static void ValidateAspNetCoreActivity(Activity activityToValidate, string expectedHttpPath)
        {
            Assert.Equal(ActivityKind.Server, activityToValidate.Kind);
            Assert.Equal(expectedHttpPath, activityToValidate.GetTagValue(SpanAttributeConstants.HttpPathKey) as string);
        }

        private static void ActivityEnrichment(Activity activity, string method, object obj)
        {
            Assert.True(activity.IsAllDataRequested);
            switch (method)
            {
                case "OnStartActivity":
                    Assert.True(obj is HttpRequest);
                    break;

                case "OnStopActivity":
                    Assert.True(obj is HttpResponse);
                    break;

                default:
                    break;
            }
        }
    }
}
