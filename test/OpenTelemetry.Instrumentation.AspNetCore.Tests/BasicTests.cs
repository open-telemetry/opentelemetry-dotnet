// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using TestApp.AspNetCore;
using TestApp.AspNetCore.Filters;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusIsUnsetOn200Response(bool disableLogging)
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
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                if (disableLogging)
                {
                    builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                }
            })
            .CreateClient())
        {
            // Act
            using var response = await client.GetAsync("/api/values");

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.Equal(200, activity.GetTagValue(SemanticConventions.AttributeHttpResponseStatusCode));
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
                        options.EnrichWithHttpRequest = (activity, request) => { activity.SetTag("enrichedOnStart", "yes"); };
                        options.EnrichWithHttpResponse = (activity, response) => { activity.SetTag("enrichedOnStop", "yes"); };
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            // Act
            using var response = await client.GetAsync("/api/values");

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        if (shouldEnrich)
        {
            Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enrichedOnStart" && tag.Value == "yes"));
            Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enrichedOnStop" && tag.Value == "yes"));
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
            {
                builder.ConfigureTestServices(services =>
                {
                    this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems)
                    .Build();
                });

                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
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

        Assert.Equal(expectedTraceId, activity.Context.TraceId);
        Assert.Equal(expectedSpanId, activity.ParentSpanId);

        ValidateAspNetCoreActivity(activity, "/api/values/2");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomPropagator(bool addSampler)
    {
        try
        {
            var exportedItems = new List<Activity>();
            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var propagator = new CustomTextMapPropagator
            {
                TraceId = expectedTraceId,
                SpanId = expectedSpanId,
            };

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    {
                        builder.ConfigureTestServices(services =>
                        {
                            Sdk.SetDefaultTextMapPropagator(propagator);
                            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

                            if (addSampler)
                            {
                                tracerProviderBuilder
                                    .SetSampler(new TestSampler(SamplingDecision.RecordAndSample, new Dictionary<string, object> { { "SomeTag", "SomeKey" }, }));
                            }

                            this.tracerProvider = tracerProviderBuilder
                                                    .AddAspNetCoreInstrumentation()
                                                    .AddInMemoryExporter(exportedItems)
                                                    .Build();
                        });
                        builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                    }))
            {
                using var client = testFactory.CreateClient();
                using var response = await client.GetAsync("/api/values/2");
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForActivityExport(exportedItems, 1);
            }

            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            Assert.True(activity.Duration != TimeSpan.Zero);

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
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var client = testFactory.CreateClient();

            // Act
            using var response1 = await client.GetAsync("/api/values");
            using var response2 = await client.GetAsync("/api/values/2");

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
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var client = testFactory.CreateClient();

            // Act
            using (var inMemoryEventListener = new InMemoryEventListener(AspNetCoreInstrumentationEventSource.Log))
            {
                using var response1 = await client.GetAsync("/api/values");
                using var response2 = await client.GetAsync("/api/values/2");

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
                    {
                        builder.ConfigureTestServices(services => { this.tracerProvider = Sdk.CreateTracerProviderBuilder().SetSampler(new TestSampler(samplingDecision)).AddAspNetCoreInstrumentation().Build(); });
                        builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                    });
            using var client = testFactory.CreateClient();

            // Test TraceContext Propagation
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityTraceContext");
            var response = await client.SendAsync(request);
            var childActivityTraceContext = JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());

            response.EnsureSuccessStatusCode();

            Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
            Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
            Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

            // Test Baggage Context Propagation
            request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

            response = await client.SendAsync(request);
            var childActivityBaggageContext = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(await response.Content.ReadAsStringAsync());

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
                {
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
                    });
                    builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });
            using var client = testFactory.CreateClient();

            // Test TraceContext Propagation
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityTraceContext");
            var response = await client.SendAsync(request);

            // Ensure that filter was called
            Assert.True(isFilterCalled);

            var childActivityTraceContext = JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());

            response.EnsureSuccessStatusCode();

            Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
            Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
            Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

            // Test Baggage Context Propagation
            request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

            response = await client.SendAsync(request);
            var childActivityBaggageContext = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(await response.Content.ReadAsStringAsync());

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
    public async Task BaggageIsNotClearedWhenActivityStopped()
    {
        int? baggageCountAfterStart = null;
        int? baggageCountAfterStop = null;
        using EventWaitHandle stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset);

        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(
                    new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                    {
                        OnEventWrittenCallback = (name, payload) =>
                        {
                            switch (name)
                            {
                                case HttpInListener.OnStartEvent:
                                    {
                                        baggageCountAfterStart = Baggage.Current.Count;
                                    }

                                    break;
                                case HttpInListener.OnStopEvent:
                                    {
                                        baggageCountAfterStop = Baggage.Current.Count;
                                        stopSignal.Set();
                                    }

                                    break;
                            }
                        },
                    })
                .Build();
        }

        // Arrange
        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
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
        Assert.Equal(2, baggageCountAfterStop);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop, false, false)]
    [InlineData(SamplingDecision.RecordOnly, true, true)]
    [InlineData(SamplingDecision.RecordAndSample, true, true)]
    public async Task FilterAndEnrichAreOnlyCalledWhenSampled(SamplingDecision samplingDecision, bool shouldFilterBeCalled, bool shouldEnrichBeCalled)
    {
        bool filterCalled = false;
        bool enrichWithHttpRequestCalled = false;
        bool enrichWithHttpResponseCalled = false;
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
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        enrichWithHttpRequestCalled = true;
                    };
                    options.EnrichWithHttpResponse = (activity, request) =>
                    {
                        enrichWithHttpResponseCalled = true;
                    };
                })
                .Build();
        }

        // Arrange
        using var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient();

        // Act
        using var response = await client.GetAsync("/api/values");

        // Assert
        Assert.Equal(shouldFilterBeCalled, filterCalled);
        Assert.Equal(shouldEnrichBeCalled, enrichWithHttpRequestCalled);
        Assert.Equal(shouldEnrichBeCalled, enrichWithHttpResponseCalled);
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
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            using var response = await client.GetAsync("/api/values/2");
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

        // tag http.method should be added on activity started by asp.net core
        Assert.Equal("GET", aspnetcoreframeworkactivity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
        Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", aspnetcoreframeworkactivity.OperationName);
    }

    [Theory]
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("PUT", "PUT")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("Get", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("TRACE", "TRACE")]
    [InlineData("CUSTOM", "_OTHER")]
    public async Task HttpRequestMethodIsSetAsPerSpec(string originalMethod, string expectedMethod)
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
        using var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient();

        var message = new HttpRequestMessage();

        message.Method = new HttpMethod(originalMethod);

        try
        {
            using var response = await client.SendAsync(message);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // ignore error.
        }

        WaitForActivityExport(exportedItems, 1);

        Assert.Single(exportedItems);

        var activity = exportedItems[0];

        Assert.Contains(activity.TagObjects, t => t.Key == SemanticConventions.AttributeHttpRequestMethod);

        if (originalMethod.Equals(expectedMethod, StringComparison.OrdinalIgnoreCase))
        {
            Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeHttpRequestMethodOriginal);
        }
        else
        {
            Assert.Equal(originalMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethodOriginal) as string);
        }

        Assert.Equal(expectedMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
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
            {
                builder.ConfigureTestServices((IServiceCollection services) =>
                {
                    services.AddSingleton<ActivityMiddleware.ActivityMiddlewareImpl>(new TestNullHostActivityMiddlewareImpl(activitySourceName, activityName));
                    services.AddOpenTelemetry()
                        .WithTracing(builder => builder
                            .AddAspNetCoreInstrumentation()
                            .AddSource(activitySourceName)
                            .AddInMemoryExporter(exportedItems));
                });
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            using var response = await client.GetAsync("/api/values/2");
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

        // tag http.method should be added on activity started by asp.net core
        Assert.Equal("GET", aspnetcoreframeworkactivity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
        Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", aspnetcoreframeworkactivity.OperationName);
    }

#if NET7_0_OR_GREATER
    [Fact]
    public async Task UserRegisteredActivitySourceIsUsedForActivityCreationByAspNetCore()
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems));

            // Register ActivitySource here so that it will be used
            // by ASP.NET Core to create activities
            // https://github.com/dotnet/aspnetcore/blob/0e5cbf447d329a1e7d69932c3decd1c70a00fbba/src/Hosting/Hosting/src/Internal/WebHost.cs#L152
            services.AddSingleton(sp => new ActivitySource("UserRegisteredActivitySource"));
        }

        // Arrange
        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            // Act
            using var response = await client.GetAsync("/api/values");

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
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(
                (s) => this.ConfigureExceptionFilters(s, mode, ref exportedItems));
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            // Act
            using var response = await client.GetAsync("/api/error");

            WaitForActivityExport(exportedItems, 1);
        }

        // Assert
        AssertException(exportedItems);
    }

    [Fact]
    public async Task DiagnosticSourceCallbacksAreReceivedOnlyForSubscribedEvents()
    {
        int numberOfUnSubscribedEvents = 0;
        int numberofSubscribedEvents = 0;

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(
                new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                {
                    OnEventWrittenCallback = (name, payload) =>
                    {
                        switch (name)
                        {
                            case HttpInListener.OnStartEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;
                            case HttpInListener.OnStopEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;
                            default:
                                {
                                    numberOfUnSubscribedEvents++;
                                }

                                break;
                        }
                    },
                })
            .Build();

        // Arrange
        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");

            // Act
            using var response = await client.SendAsync(request);
        }

        Assert.Equal(0, numberOfUnSubscribedEvents);
        Assert.Equal(2, numberofSubscribedEvents);
    }

    [Fact]
    public async Task DiagnosticSourceExceptionCallbackIsReceivedForUnHandledException()
    {
        int numberOfUnSubscribedEvents = 0;
        int numberofSubscribedEvents = 0;
        int numberOfExceptionCallbacks = 0;

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(
                new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                {
                    OnEventWrittenCallback = (name, payload) =>
                    {
                        switch (name)
                        {
                            case HttpInListener.OnStartEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;
                            case HttpInListener.OnStopEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;

                            // TODO: Add test case for validating name for both the types
                            // of exception event.
                            case HttpInListener.OnUnhandledHostingExceptionEvent:
                            case HttpInListener.OnUnHandledDiagnosticsExceptionEvent:
                                {
                                    numberofSubscribedEvents++;
                                    numberOfExceptionCallbacks++;
                                }

                                break;
                            default:
                                {
                                    numberOfUnSubscribedEvents++;
                                }

                                break;
                        }
                    },
                })
            .Build();

        // Arrange
        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
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
        Assert.Equal(0, numberOfUnSubscribedEvents);
        Assert.Equal(3, numberofSubscribedEvents);
    }

    [Fact]
    public async Task DiagnosticSourceExceptionCallBackIsNotReceivedForExceptionsHandledInMiddleware()
    {
        int numberOfUnSubscribedEvents = 0;
        int numberOfSubscribedEvents = 0;
        int numberOfExceptionCallbacks = 0;
        bool exceptionHandled = false;

        // configure SDK
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(
                new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                {
                    OnEventWrittenCallback = (name, payload) =>
                    {
                        switch (name)
                        {
                            case HttpInListener.OnStartEvent:
                                {
                                    numberOfSubscribedEvents++;
                                }

                                break;
                            case HttpInListener.OnStopEvent:
                                {
                                    numberOfSubscribedEvents++;
                                }

                                break;

                            // TODO: Add test case for validating name for both the types
                            // of exception event.
                            case HttpInListener.OnUnhandledHostingExceptionEvent:
                            case HttpInListener.OnUnHandledDiagnosticsExceptionEvent:
                                {
                                    numberOfSubscribedEvents++;
                                    numberOfExceptionCallbacks++;
                                }

                                break;
                            default:
                                {
                                    numberOfUnSubscribedEvents++;
                                }

                                break;
                        }
                    },
                })
                .Build();

        TestMiddleware.Create(builder => builder
            .UseExceptionHandler(handler =>
                handler.Run(async (ctx) =>
                {
                    exceptionHandled = true;
                    await ctx.Response.WriteAsync("handled");
                })));

        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/error");
                using var response = await client.SendAsync(request);
            }
            catch
            {
                // ignore exception
            }
        }

        Assert.Equal(0, numberOfExceptionCallbacks);
        Assert.Equal(0, numberOfUnSubscribedEvents);
        Assert.Equal(2, numberOfSubscribedEvents);
        Assert.True(exceptionHandled);
    }

    [Theory]
    [InlineData("GET", null)]
    public async Task KnownHttpMethodsAreBeingRespected_Defaults(string expectedMethod, string expectedOriginalMethod)
    {
        var exportedItems = new List<Activity>();

        using var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient();

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");
        using var response = await client.SendAsync(request);

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.Equal(expectedMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
        Assert.Equal(expectedOriginalMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethodOriginal) as string);
    }

    [Theory]
    [InlineData("GET,POST,PUT", "GET", null)]
    [InlineData("get,post,put", "get", null)]
    [InlineData("POST,PUT", "_OTHER", "GET")]
    [InlineData("post,put", "_OTHER", "GET")]
    [InlineData("fooBar", "_OTHER", "GET")]
    [InlineData("fooBar,POST", "_OTHER", "GET")]
    [InlineData(",", "_OTHER", "GET")]
    public async Task KnownHttpMethodsAreBeingRespected_EnvVar(string knownMethods, string expectedMethod, string expectedOriginalMethod)
    {
        var config = new KeyValuePair<string, string>[] { new("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", knownMethods) };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var exportedItems = new List<Activity>();

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");
        using var response = await client.SendAsync(request);

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        Assert.Equal(expectedMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
        Assert.Equal(expectedOriginalMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethodOriginal) as string);
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
        Assert.Equal(expectedHttpPath, activityToValidate.GetTagValue(SemanticConventions.AttributeUrlPath) as string);
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

    private class ExtractOnlyPropagator(ActivityContext activityContext, Baggage baggage) : TextMapPropagator
    {
        private readonly ActivityContext activityContext = activityContext;
        private readonly Baggage baggage = baggage;

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

    private class TestSampler(SamplingDecision samplingDecision, IEnumerable<KeyValuePair<string, object>> attributes = null) : Sampler
    {
        private readonly SamplingDecision samplingDecision = samplingDecision;
        private readonly IEnumerable<KeyValuePair<string, object>> attributes = attributes;

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            return new SamplingResult(this.samplingDecision, this.attributes);
        }
    }

    private class TestHttpInListener(AspNetCoreTraceInstrumentationOptions options)
        : HttpInListener(options, new RequestMethodHelper(new ConfigurationBuilder().Build()))
    {
        public Action<string, object> OnEventWrittenCallback;

        public override void OnEventWritten(string name, object payload)
        {
            base.OnEventWritten(name, payload);

            this.OnEventWrittenCallback?.Invoke(name, payload);
        }
    }

    private class TestNullHostActivityMiddlewareImpl(string activitySourceName, string activityName) : ActivityMiddleware.ActivityMiddlewareImpl
    {
        private readonly ActivitySource activitySource = new(activitySourceName);
        private readonly string activityName = activityName;
        private Activity activity;

        public override void PreProcess(HttpContext context)
        {
            // Setting the host activity i.e. activity started by asp.net core
            // to null here will have no impact on middleware activity.
            // This also means that asp.net core activity will not be found
            // during OnEventWritten event.
            Activity.Current = null;
            this.activity = this.activitySource.StartActivity(this.activityName);
        }

        public override void PostProcess(HttpContext context)
        {
            this.activity?.Stop();
        }
    }

    private class TestActivityMiddlewareImpl(string activitySourceName, string activityName) : ActivityMiddleware.ActivityMiddlewareImpl
    {
        private readonly ActivitySource activitySource = new(activitySourceName);
        private readonly string activityName = activityName;
        private Activity activity;

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
