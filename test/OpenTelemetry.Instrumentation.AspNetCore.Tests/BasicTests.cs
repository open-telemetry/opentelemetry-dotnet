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
using Newtonsoft.Json;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
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
    // See https://github.com/aspnet/Docs/tree/master/aspnetcore/test/integration-tests/samples/2.x/IntegrationTestsSample
    public sealed class BasicTests
        : IClassFixture<WebApplicationFactory<Startup>>, IDisposable
    {
        private readonly WebApplicationFactory<Startup> factory;
        private TracerProvider tracerProvider = null;

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
            var exportedItems = new List<Activity>();
            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems)
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

                WaitForActivityExport(exportedItems, 1);
            }

            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            Assert.Equal(200, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));

            var status = activity.GetStatus();
            Assert.Equal(status, Status.Unset);

            ValidateAspNetCoreActivity(activity, "/api/values");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan(bool shouldEnrich)
        {
            var exportedItems = new List<Activity>();
            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        if (shouldEnrich)
                        {
                            options.Enrich = ActivityEnrichment;
                        }
                    })
                    .AddInMemoryExporter(exportedItems)
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

                WaitForActivityExport(exportedItems, 1);
            }

            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            if (shouldEnrich)
            {
                Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enriched" && tag.Value == "yes"));
            }

            ValidateAspNetCoreActivity(activity, "/api/values");
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallUsesParentContext()
        {
            var exportedItems = new List<Activity>();
            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        this.tracerProvider = Sdk.CreateTracerProviderBuilder().AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems)
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

                WaitForActivityExport(exportedItems, 1);
            }

            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", activity.OperationName);
            Assert.Equal("api/Values/{id}", activity.DisplayName);

            Assert.Equal(expectedTraceId, activity.Context.TraceId);
            Assert.Equal(expectedSpanId, activity.ParentSpanId);

            ValidateAspNetCoreActivity(activity, "/api/values/2");
        }

        [Fact]
        public async Task CustomPropagator()
        {
            try
            {
                var exportedItems = new List<Activity>();
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
                            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                                .AddAspNetCoreInstrumentation()
                                .AddInMemoryExporter(exportedItems)
                                .Build();
                        })))
                {
                    using var client = testFactory.CreateClient();
                    var response = await client.GetAsync("/api/values/2");
                    response.EnsureSuccessStatusCode(); // Status Code 200-299

                    WaitForActivityExport(exportedItems, 1);
                }

                Assert.Single(exportedItems);
                var activity = exportedItems[0];

                Assert.True(activity.Duration != TimeSpan.Zero);
                Assert.Equal("api/Values/{id}", activity.DisplayName);

                Assert.Equal(expectedTraceId, activity.Context.TraceId);
                Assert.Equal(expectedSpanId, activity.ParentSpanId);

                ValidateAspNetCoreActivity(activity, "/api/values/2");
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }

        [Fact]
        public async Task RequestNotCollectedWhenFilterIsApplied()
        {
            var exportedItems = new List<Activity>();

            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation((opt) => opt.Filter = (ctx) => ctx.Request.Path != "/api/values/2")
                    .AddInMemoryExporter(exportedItems)
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

                WaitForActivityExport(exportedItems, 1);
            }

            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            ValidateAspNetCoreActivity(activity, "/api/values");
        }

        [Fact]
        public async Task RequestNotCollectedWhenFilterThrowException()
        {
            var exportedItems = new List<Activity>();

            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
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
                    .AddInMemoryExporter(exportedItems)
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

                WaitForActivityExport(exportedItems, 1);
            }

            // As InstrumentationFilter threw, we continue as if the
            // InstrumentationFilter did not exist.

            Assert.Single(exportedItems);
            var activity = exportedItems[0];
            ValidateAspNetCoreActivity(activity, "/api/values");
        }

        [Theory]
        [InlineData(SamplingDecision.Drop)]
        [InlineData(SamplingDecision.RecordOnly)]
        [InlineData(SamplingDecision.RecordAndSample)]
        public async Task ExtractContextIrrespectiveOfSamplingDecision(SamplingDecision samplingDecision)
        {
            try
            {
                var expectedTraceId = ActivityTraceId.CreateRandom();
                var expectedParentSpanId = ActivitySpanId.CreateRandom();
                var expectedTraceState = "rojo=1,congo=2";
                var activityContext = new ActivityContext(expectedTraceId, expectedParentSpanId, ActivityTraceFlags.Recorded, expectedTraceState);
                var expectedBaggage = Baggage.SetBaggage("key1", "value1").SetBaggage("key2", "value2");
                Sdk.SetDefaultTextMapPropagator(new ExtractOnlyPropagator(activityContext, expectedBaggage));

                // Arrange
                using var testFactory = this.factory
                    .WithWebHostBuilder(builder =>
                        builder.ConfigureTestServices(services =>
                        {
                            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                                .SetSampler(new TestSampler(samplingDecision))
                                .AddAspNetCoreInstrumentation()
                                .Build();
                        }));
                using var client = testFactory.CreateClient();

                // Test TraceContext Propagation
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityTraceContext");
                var response = await client.SendAsync(request);
                var childActivityTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

                response.EnsureSuccessStatusCode();

                Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
                Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
                Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

                // Test Baggage Context Propagation
                request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

                response = await client.SendAsync(request);
                var childActivityBaggageContext = JsonConvert.DeserializeObject<IReadOnlyDictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

                response.EnsureSuccessStatusCode();

                Assert.Single(childActivityBaggageContext, item => item.Key == "key1" && item.Value == "value1");
                Assert.Single(childActivityBaggageContext, item => item.Key == "key2" && item.Value == "value2");
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }

        [Fact]
        public async Task ExtractContextIrrespectiveOfTheFilterApplied()
        {
            try
            {
                var expectedTraceId = ActivityTraceId.CreateRandom();
                var expectedParentSpanId = ActivitySpanId.CreateRandom();
                var expectedTraceState = "rojo=1,congo=2";
                var activityContext = new ActivityContext(expectedTraceId, expectedParentSpanId, ActivityTraceFlags.Recorded, expectedTraceState);
                var expectedBaggage = Baggage.SetBaggage("key1", "value1").SetBaggage("key2", "value2");
                Sdk.SetDefaultTextMapPropagator(new ExtractOnlyPropagator(activityContext, expectedBaggage));

                // Arrange
                bool isFilterCalled = false;
                using var testFactory = this.factory
                    .WithWebHostBuilder(builder =>
                        builder.ConfigureTestServices(services =>
                        {
                            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                                .AddAspNetCoreInstrumentation(options =>
                                {
                                    options.Filter = context =>
                                    {
                                        isFilterCalled = true;
                                        return false;
                                    };
                                })
                                .Build();
                        }));
                using var client = testFactory.CreateClient();

                // Test TraceContext Propagation
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityTraceContext");
                var response = await client.SendAsync(request);

                // Ensure that filter was called
                Assert.True(isFilterCalled);

                var childActivityTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

                response.EnsureSuccessStatusCode();

                Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
                Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
                Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

                // Test Baggage Context Propagation
                request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

                response = await client.SendAsync(request);
                var childActivityBaggageContext = JsonConvert.DeserializeObject<IReadOnlyDictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

                response.EnsureSuccessStatusCode();

                Assert.Single(childActivityBaggageContext, item => item.Key == "key1" && item.Value == "value1");
                Assert.Single(childActivityBaggageContext, item => item.Key == "key2" && item.Value == "value2");
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }

        [Fact]
        public async Task BaggageClearedWhenActivityStopped()
        {
            int? baggageCountAfterStart = null;
            int? baggageCountAfterStop = null;
            using EventWaitHandle stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset);

            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation(new AspNetCoreInstrumentation(
                        new TestHttpInListener(new AspNetCoreInstrumentationOptions())
                        {
                            OnStartActivityCallback = (activity, payload) =>
                            {
                                baggageCountAfterStart = Baggage.Current.Count;
                            },
                            OnStopActivityCallback = (activity, payload) =>
                            {
                                baggageCountAfterStop = Baggage.Current.Count;
                                stopSignal.Set();
                            },
                        }))
                    .Build();
            }

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices))
                .CreateClient())
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");

                request.Headers.TryAddWithoutValidation("baggage", "TestKey1=123,TestKey2=456");

                // Act
                using var response = await client.SendAsync(request);
            }

            stopSignal.WaitOne(5000);

            // Assert
            Assert.NotNull(baggageCountAfterStart);
            Assert.Equal(2, baggageCountAfterStart);
            Assert.NotNull(baggageCountAfterStop);
            Assert.Equal(0, baggageCountAfterStop);
        }

        [Theory]
        [InlineData(SamplingDecision.Drop, false, false)]
        [InlineData(SamplingDecision.RecordOnly, true, true)]
        [InlineData(SamplingDecision.RecordAndSample, true, true)]
        public async Task FilterAndEnrichAreOnlyCalledWhenSampled(SamplingDecision samplingDecision, bool shouldFilterBeCalled, bool shouldEnrichBeCalled)
        {
            bool filterCalled = false;
            bool enrichCalled = false;
            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new TestSampler(samplingDecision))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = (context) =>
                        {
                            filterCalled = true;
                            return true;
                        };
                        options.Enrich = (activity, methodName, request) =>
                        {
                            enrichCalled = true;
                        };
                    })
                    .Build();
            }

            // Arrange
            using var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices))
                .CreateClient();

            // Act
            var response = await client.GetAsync("/api/values");

            // Assert
            Assert.Equal(shouldFilterBeCalled, filterCalled);
            Assert.Equal(shouldEnrichBeCalled, enrichCalled);
        }

        public void Dispose()
        {
            this.tracerProvider?.Dispose();
        }

        private static void WaitForActivityExport(List<Activity> exportedItems, int count)
        {
            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so
            // giving some breezing room for the End callback to complete
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return exportedItems.Count >= count;
                },
                TimeSpan.FromSeconds(1)));
        }

        private static void ValidateAspNetCoreActivity(Activity activityToValidate, string expectedHttpPath)
        {
            Assert.Equal(ActivityKind.Server, activityToValidate.Kind);
            Assert.Equal(HttpInListener.ActivitySourceName, activityToValidate.Source.Name);
            Assert.Equal(HttpInListener.Version.ToString(), activityToValidate.Source.Version);
            Assert.Equal(expectedHttpPath, activityToValidate.GetTagValue(SemanticConventions.AttributeHttpTarget) as string);
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

            activity.SetTag("enriched", "yes");
        }

        private class ExtractOnlyPropagator : TextMapPropagator
        {
            private readonly ActivityContext activityContext;
            private readonly Baggage baggage;

            public ExtractOnlyPropagator(ActivityContext activityContext, Baggage baggage)
            {
                this.activityContext = activityContext;
                this.baggage = baggage;
            }

            public override ISet<string> Fields => throw new NotImplementedException();

            public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
            {
                return new PropagationContext(this.activityContext, this.baggage);
            }

            public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
            {
                throw new NotImplementedException();
            }
        }

        private class TestSampler : Sampler
        {
            private readonly SamplingDecision samplingDecision;

            public TestSampler(SamplingDecision samplingDecision)
            {
                this.samplingDecision = samplingDecision;
            }

            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                return new SamplingResult(this.samplingDecision);
            }
        }

        private class TestHttpInListener : HttpInListener
        {
            public Action<Activity, object> OnStartActivityCallback;

            public Action<Activity, object> OnStopActivityCallback;

            public TestHttpInListener(AspNetCoreInstrumentationOptions options)
                : base(options)
            {
            }

            public override void OnStartActivity(Activity activity, object payload)
            {
                base.OnStartActivity(activity, payload);

                this.OnStartActivityCallback?.Invoke(activity, payload);
            }

            public override void OnStopActivity(Activity activity, object payload)
            {
                base.OnStopActivity(activity, payload);

                this.OnStopActivityCallback?.Invoke(activity, payload);
            }
        }
    }
}
