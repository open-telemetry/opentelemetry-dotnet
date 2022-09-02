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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using TestApp.AspNetCore;
using TestApp.AspNetCore.Filters;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    // See https://github.com/aspnet/Docs/tree/master/aspnetcore/test/integration-tests/samples/2.x/IntegrationTestsSample
    public sealed class BasicTests
        : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        private TracerProvider tracerProvider = null;

        public BasicTests(WebApplicationFactory<Program> factory)
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
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
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
                var activityContext = new ActivityContext(expectedTraceId, expectedParentSpanId, ActivityTraceFlags.Recorded, expectedTraceState, true);
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
                var childActivityTraceContext = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

                response.EnsureSuccessStatusCode();

                Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
                Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
                Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

                // Test Baggage Context Propagation
                request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

                response = await client.SendAsync(request);
                var childActivityBaggageContext = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

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

                var childActivityTraceContext = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

                response.EnsureSuccessStatusCode();

                Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
                Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
                Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

                // Test Baggage Context Propagation
                request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

                response = await client.SendAsync(request);
                var childActivityBaggageContext = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(response.Content.ReadAsStringAsync().Result);

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

        [Fact]
        public async Task ActivitiesStartedInMiddlewareShouldNotBeUpdated()
        {
            var exportedItems = new List<Activity>();

            var activitySourceName = "TestMiddlewareActivitySource";
            var activityName = "TestMiddlewareActivity";

            void ConfigureTestServices(IServiceCollection services)
            {
                services.AddSingleton<ActivityMiddleware.ActivityMiddlewareImpl>(new TestActivityMiddlewareImpl(activitySourceName, activityName));
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .AddSource(activitySourceName)
                    .AddInMemoryExporter(exportedItems)
                    .Build();
            }

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices))
                .CreateClient())
            {
                var response = await client.GetAsync("/api/values/2");
                response.EnsureSuccessStatusCode();
                WaitForActivityExport(exportedItems, 2);
            }

            Assert.Equal(2, exportedItems.Count);

            var middlewareActivity = exportedItems[0];

            var aspnetcoreframeworkactivity = exportedItems[1];

            // Middleware activity name should not be changed
            Assert.Equal(ActivityKind.Internal, middlewareActivity.Kind);
            Assert.Equal(activityName, middlewareActivity.OperationName);
            Assert.Equal(activityName, middlewareActivity.DisplayName);

            // tag http.route should be added on activity started by asp.net core
            Assert.Equal("api/Values/{id}", aspnetcoreframeworkactivity.GetTagValue(SemanticConventions.AttributeHttpRoute) as string);
            Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", aspnetcoreframeworkactivity.OperationName);
            Assert.Equal("api/Values/{id}", aspnetcoreframeworkactivity.DisplayName);
        }

        [Fact]
        public async Task ActivitiesStartedInMiddlewareBySettingHostActivityToNullShouldNotBeUpdated()
        {
            var exportedItems = new List<Activity>();

            var activitySourceName = "TestMiddlewareActivitySource";
            var activityName = "TestMiddlewareActivity";

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices((IServiceCollection services) =>
                    {
                        services.AddSingleton<ActivityMiddleware.ActivityMiddlewareImpl>(new TestNullHostActivityMiddlewareImpl(activitySourceName, activityName));
                        services.AddOpenTelemetryTracing((builder) => builder.AddAspNetCoreInstrumentation()
                        .AddSource(activitySourceName)
                        .AddInMemoryExporter(exportedItems));
                    }))
                .CreateClient())
            {
                var response = await client.GetAsync("/api/values/2");
                response.EnsureSuccessStatusCode();
                WaitForActivityExport(exportedItems, 2);
            }

            Assert.Equal(2, exportedItems.Count);

            var middlewareActivity = exportedItems[0];

            var aspnetcoreframeworkactivity = exportedItems[1];

            // Middleware activity name should not be changed
            Assert.Equal(ActivityKind.Internal, middlewareActivity.Kind);
            Assert.Equal(activityName, middlewareActivity.OperationName);
            Assert.Equal(activityName, middlewareActivity.DisplayName);

            // tag http.route should not be added on activity started by asp.net core as it will not be found during oncustom event
            Assert.DoesNotContain(aspnetcoreframeworkactivity.TagObjects, t => t.Key == SemanticConventions.AttributeHttpRoute);
            Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", aspnetcoreframeworkactivity.OperationName);
            Assert.Equal("/api/values/2", aspnetcoreframeworkactivity.DisplayName);
        }

#if NET7_0_OR_GREATER
        [Fact]
        public async Task UserRegisteredActivitySourceIsUsedForActivityCreationByAspNetCore()
        {
            var exportedItems = new List<Activity>();
            void ConfigureTestServices(IServiceCollection services)
            {
                services.AddOpenTelemetryTracing(options =>
                {
                    options.AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems);
                });

                // Register ActivitySource here so that it will be used
                // by ASP.NET Core to create activities
                // https://github.com/dotnet/aspnetcore/blob/0e5cbf447d329a1e7d69932c3decd1c70a00fbba/src/Hosting/Hosting/src/Internal/WebHost.cs#L152
                services.AddSingleton(sp => new ActivitySource("UserRegisteredActivitySource"));
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

            Assert.Equal("UserRegisteredActivitySource", activity.Source.Name);
        }
#endif

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task ShouldExportActivityWithOneOrMoreExceptionFilters(int mode)
        {
            var exportedItems = new List<Activity>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    (s) => this.ConfigureExceptionFilters(s, mode, ref exportedItems)))
                .CreateClient())
            {
                // Act
                var response = await client.GetAsync("/api/error");

                WaitForActivityExport(exportedItems, 1);
            }

            // Assert
            AssertException(exportedItems);
        }

        [Fact(Skip = "Pending Changes https://github.com/open-telemetry/opentelemetry-dotnet/issues/3495")]
        public async Task DiagnosticSourceCustomCallbacksAreReceivedOnlyForSubscribedEvents()
        {
            int numberOfCustomCallbacks = 0;
            string expectedCustomEventName = "Microsoft.AspNetCore.Mvc.BeforeAction";
            string actualCustomEventName = null;
            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation(new AspNetCoreInstrumentation(
                        new TestHttpInListener(new AspNetCoreInstrumentationOptions())
                        {
                            OnCustomCallback = (name, activity, payload) =>
                            {
                                actualCustomEventName = name;
                                numberOfCustomCallbacks++;
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

                // Act
                using var response = await client.SendAsync(request);
            }

            Assert.Equal(1, numberOfCustomCallbacks);
            Assert.Equal(expectedCustomEventName, actualCustomEventName);
        }

        [Fact]
        public async Task DiagnosticSourceExceptionCallbackIsReceivedForUnHandledException()
        {
            int numberOfExceptionCallbacks = 0;
            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation(new AspNetCoreInstrumentation(
                        new TestHttpInListener(new AspNetCoreInstrumentationOptions())
                        {
                            OnExceptionCallback = (activity, payload) =>
                            {
                                numberOfExceptionCallbacks++;
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
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/error");

                    // Act
                    using var response = await client.SendAsync(request);
                }
                catch
                {
                    // ignore exception
                }
            }

            Assert.Equal(1, numberOfExceptionCallbacks);
        }

        [Fact(Skip = "Pending Changes https://github.com/open-telemetry/opentelemetry-dotnet/issues/3495")]
        public async Task DiagnosticSourceExceptionCallBackIsNotReceivedForExceptionsHandledInMiddleware()
        {
            int numberOfExceptionCallbacks = 0;

            // configure SDK
            using var tracerprovider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(new AspNetCoreInstrumentation(
                    new TestHttpInListener(new AspNetCoreInstrumentationOptions())
                    {
                        OnExceptionCallback = (activity, payload) =>
                        {
                            numberOfExceptionCallbacks++;
                        },
                    }))
                    .Build();

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.UseExceptionHandler(handler =>
            {
                handler.Run(async (ctx) =>
                {
                    await ctx.Response.WriteAsync("handled");
                });
            });

            app.Map("/error", ThrowException);

            static void ThrowException(IApplicationBuilder app)
            {
                app.Run(context =>
                {
                    throw new Exception("CustomException");
                });
            }

            _ = app.RunAsync();

            using var client = new HttpClient();
            try
            {
                await client.GetStringAsync("http://localhost:5000/error");
            }
            catch
            {
                // ignore 500 error.
            }

            Assert.Equal(0, numberOfExceptionCallbacks);
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
#if NET7_0_OR_GREATER
            Assert.Equal(HttpInListener.AspNetCoreActivitySourceName, activityToValidate.Source.Name);
            Assert.Empty(activityToValidate.Source.Version);
#else
            Assert.Equal(HttpInListener.ActivitySourceName, activityToValidate.Source.Name);
            Assert.Equal(HttpInListener.Version.ToString(), activityToValidate.Source.Version);
#endif
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

        private static void AssertException(List<Activity> exportedItems)
        {
            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            var exMessage = "something's wrong!";
            Assert.Single(activity.Events);
            Assert.Equal("System.Exception", activity.Events.First().Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
            Assert.Equal(exMessage, activity.Events.First().Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);

            ValidateAspNetCoreActivity(activity, "/api/error");
        }

        private void ConfigureExceptionFilters(IServiceCollection services, int mode, ref List<Activity> exportedItems)
        {
            switch (mode)
            {
                case 1:
                    services.AddMvc(x => x.Filters.Add<ExceptionFilter1>());
                    break;
                case 2:
                    services.AddMvc(x => x.Filters.Add<ExceptionFilter1>());
                    services.AddMvc(x => x.Filters.Add<ExceptionFilter2>());
                    break;
                default:
                    break;
            }

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(x => x.RecordException = true)
                .AddInMemoryExporter(exportedItems)
                .Build();
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

            public Action<Activity, object> OnExceptionCallback;

            public Action<string, Activity, object> OnCustomCallback;

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

            public override void OnCustom(string name, Activity activity, object payload)
            {
                base.OnCustom(name, activity, payload);

                this.OnCustomCallback?.Invoke(name, activity, payload);
            }

            public override void OnException(Activity activity, object payload)
            {
                base.OnException(activity, payload);

                this.OnExceptionCallback?.Invoke(activity, payload);
            }
        }

        private class TestNullHostActivityMiddlewareImpl : ActivityMiddleware.ActivityMiddlewareImpl
        {
            private ActivitySource activitySource;
            private Activity activity;
            private string activityName;

            public TestNullHostActivityMiddlewareImpl(string activitySourceName, string activityName)
            {
                this.activitySource = new ActivitySource(activitySourceName);
                this.activityName = activityName;
            }

            public override void PreProcess(HttpContext context)
            {
                // Setting the host activity i.e. activity started by asp.net core
                // to null here will have no impact on middleware activity.
                // This also means that asp.net core activity will not be found
                // during OnCustom event.
                Activity.Current = null;
                this.activity = this.activitySource.StartActivity(this.activityName);
            }

            public override void PostProcess(HttpContext context)
            {
                this.activity?.Stop();
            }
        }

        private class TestActivityMiddlewareImpl : ActivityMiddleware.ActivityMiddlewareImpl
        {
            private ActivitySource activitySource;
            private Activity activity;
            private string activityName;

            public TestActivityMiddlewareImpl(string activitySourceName, string activityName)
            {
                this.activitySource = new ActivitySource(activitySourceName);
                this.activityName = activityName;
            }

            public override void PreProcess(HttpContext context)
            {
                this.activity = this.activitySource.StartActivity(this.activityName);
            }

            public override void PostProcess(HttpContext context)
            {
                this.activity?.Stop();
            }
        }
    }
}
