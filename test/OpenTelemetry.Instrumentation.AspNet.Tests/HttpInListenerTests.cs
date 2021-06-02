// <copyright file="HttpInListenerTests.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Routing;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests
{
    public class HttpInListenerTests : IDisposable
    {
        private readonly FakeAspNetDiagnosticSource fakeAspNetDiagnosticSource;

        public HttpInListenerTests()
        {
            this.fakeAspNetDiagnosticSource = new FakeAspNetDiagnosticSource();
        }

        public void Dispose()
        {
            this.fakeAspNetDiagnosticSource.Dispose();
        }

        [Theory]
        [InlineData("http://localhost/", "http://localhost/", 0, null, "TraceContext")]
        [InlineData("http://localhost/", "http://localhost/", 0, null, "TraceContext", true)]
        [InlineData("https://localhost/", "https://localhost/", 0, null, "TraceContext")]
        [InlineData("https://localhost/", "https://user:pass@localhost/", 0, null, "TraceContext")] // Test URL sanitization
        [InlineData("http://localhost:443/", "http://localhost:443/", 0, null, "TraceContext")] // Test http over 443
        [InlineData("https://localhost:80/", "https://localhost:80/", 0, null, "TraceContext")] // Test https over 80
        [InlineData("https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", "https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", 0, null, "TraceContext")] // Test complex URL
        [InlineData("https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", "https://user:password@localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", 0, null, "TraceContext")] // Test complex URL sanitization
        [InlineData("http://localhost:80/Index", "http://localhost:80/Index", 1, "{controller}/{action}/{id}", "TraceContext")]
        [InlineData("https://localhost:443/about_attr_route/10", "https://localhost:443/about_attr_route/10", 2, "about_attr_route/{customerId}", "TraceContext")]
        [InlineData("http://localhost:1880/api/weatherforecast", "http://localhost:1880/api/weatherforecast", 3, "api/{controller}/{id}", "TraceContext")]
        [InlineData("https://localhost:1843/subroute/10", "https://localhost:1843/subroute/10", 4, "subroute/{customerId}", "TraceContext")]
        [InlineData("http://localhost/api/value", "http://localhost/api/value", 0, null, "TraceContext", false, "/api/value")] // Request will be filtered
        [InlineData("http://localhost/api/value", "http://localhost/api/value", 0, null, "TraceContext", false, "{ThrowException}")] // Filter user code will throw an exception
        [InlineData("http://localhost/api/value/2", "http://localhost/api/value/2", 0, null, "CustomContextMatchParent")]
        [InlineData("http://localhost/api/value/2", "http://localhost/api/value/2", 0, null, "CustomContextNonmatchParent")]
        [InlineData("http://localhost/api/value/2", "http://localhost/api/value/2", 0, null, "CustomContextNonmatchParent", false, null, true)]
        public void AspNetRequestsAreCollectedSuccessfully(
            string expectedUrl,
            string url,
            int routeType,
            string routeTemplate,
            string carrierFormat,
            bool setStatusToErrorInEnrich = false,
            string filter = null,
            bool restoreCurrentActivity = false)
        {
            IDisposable openTelemetry = null;
            RouteData routeData;
            switch (routeType)
            {
                case 0: // WebForm, no route data.
                    routeData = new RouteData();
                    break;
                case 1: // Traditional MVC.
                case 2: // Attribute routing MVC.
                case 3: // Traditional WebAPI.
                    routeData = new RouteData()
                    {
                        Route = new Route(routeTemplate, null),
                    };
                    break;
                case 4: // Attribute routing WebAPI.
                    routeData = new RouteData();
                    var value = new[]
                        {
                            new
                            {
                                Route = new
                                {
                                    RouteTemplate = routeTemplate,
                                },
                            },
                        };
                    routeData.Values.Add(
                        "MS_SubRoutes",
                        value);
                    break;
                default:
                    throw new NotSupportedException();
            }

            var workerRequest = new Mock<HttpWorkerRequest>();
            workerRequest.Setup(wr => wr.GetKnownRequestHeader(It.IsAny<int>())).Returns<int>(i =>
            {
                return i switch
                {
                    39 => "Test", // User-Agent
                    _ => null,
                };
            });

            HttpContext.Current = new HttpContext(
                new HttpRequest(string.Empty, url, string.Empty)
                {
                    RequestContext = new RequestContext()
                    {
                        RouteData = routeData,
                    },
                },
                new HttpResponse(new StringWriter()));

            typeof(HttpRequest).GetField("_wr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(HttpContext.Current.Request, workerRequest.Object);

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();
            var propagator = new Mock<TextMapPropagator>();
            propagator.Setup(m => m.Extract<HttpRequest>(It.IsAny<PropagationContext>(), It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>())).Returns(new PropagationContext(
                new ActivityContext(
                    expectedTraceId,
                    expectedSpanId,
                    ActivityTraceFlags.Recorded,
                    isRemote: true),
                default));

            var activity = new Activity(HttpInListener.ActivityOperationName);
            if (carrierFormat == "TraceContext" || carrierFormat == "CustomContextMatchParent")
            {
                activity.SetParentId(expectedTraceId, expectedSpanId, ActivityTraceFlags.Recorded);
            }

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            Sdk.SetDefaultTextMapPropagator(propagator.Object);
            using (openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddAspNetInstrumentation(
                (options) =>
                {
                    options.Filter = httpContext =>
                    {
                        Assert.True(Activity.Current.IsAllDataRequested);
                        if (string.IsNullOrEmpty(filter))
                        {
                            return true;
                        }

                        if (filter == "{ThrowException}")
                        {
                            throw new InvalidOperationException();
                        }

                        return httpContext.Request.Path != filter;
                    };

                    if (setStatusToErrorInEnrich)
                    {
                        options.Enrich = GetEnrichmentAction(Status.Error);
                    }
                    else
                    {
                        options.Enrich = GetEnrichmentAction(default);
                    }
                })
            .AddProcessor(activityProcessor.Object).Build())
            {
                activity.Start();

                using (var inMemoryEventListener = new InMemoryEventListener(AspNetInstrumentationEventSource.Log))
                {
                    this.fakeAspNetDiagnosticSource.Write("Start", null);

                    if (filter == "{ThrowException}")
                    {
                        Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 3));
                    }
                }

                if (restoreCurrentActivity)
                {
                    Activity.Current = activity;
                }

                this.fakeAspNetDiagnosticSource.Write("Stop", null);

                // The above line fires DS event which is listened by Instrumentation.
                // Validate that Current activity is still the one created by Asp.Net
                Assert.Equal(HttpInListener.ActivityOperationName, Activity.Current.OperationName);
                activity.Stop();
            }

            if (HttpContext.Current.Request.Path == filter || filter == "{ThrowException}")
            {
                // only SetParentProvider/Shutdown/Dispose/OnStart are called because request was filtered.
                Assert.Equal(4, activityProcessor.Invocations.Count);
                return;
            }

            // Validate that Activity.Current is always the one created by Asp.Net
            var currentActivity = Activity.Current;

            Activity span;
            if (carrierFormat == "CustomContextNonmatchParent")
            {
                Assert.Equal(6, activityProcessor.Invocations.Count); // SetParentProvider/OnStart(framework activity)/OnStart(sibling activity)/OnEnd(sibling activity)/OnShutdown/Dispose called.

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
            }
            else
            {
                Assert.Equal(5, activityProcessor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.

                var startedActivities = activityProcessor.Invocations.Where(invo => invo.Method.Name == "OnStart");
                var stoppedActivities = activityProcessor.Invocations.Where(invo => invo.Method.Name == "OnEnd");

                // There is no sibling activity created
                Assert.Single(startedActivities);
                Assert.Single(stoppedActivities);

                Assert.Contains(startedActivities, item =>
                {
                    var startedActivity = item.Arguments[0] as Activity;
                    return startedActivity.OperationName == HttpInListener.ActivityOperationName;
                });

                // Only the sibling activity is sent to Processor.OnEnd
                Assert.Contains(stoppedActivities, item =>
                {
                    var stoppedActivity = item.Arguments[0] as Activity;
                    return stoppedActivity.OperationName == HttpInListener.ActivityOperationName;
                });
            }

            span = (Activity)activityProcessor.Invocations[2].Arguments[0];

            Assert.Equal(
                carrierFormat == "TraceContext" || carrierFormat == "CustomContextMatchParent"
                    ? HttpInListener.ActivityOperationName
                    : HttpInListener.ActivityNameByHttpInListener,
                span.OperationName);
            Assert.NotEqual(TimeSpan.Zero, span.Duration);
            Assert.Equal(expectedTraceId, span.TraceId);
            Assert.Equal(expectedSpanId, span.ParentSpanId);

            Assert.Equal(routeTemplate ?? HttpContext.Current.Request.Path, span.DisplayName);
            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.True(span.Duration != TimeSpan.Zero);

            Assert.Equal(200, span.GetTagValue(SemanticConventions.AttributeHttpStatusCode));

            if (setStatusToErrorInEnrich)
            {
                // This validates that users can override the
                // status in Enrich.
                Assert.Equal(Status.Error, span.GetStatus());

                // Instrumentation is not expected to set status description
                // as the reason can be inferred from SemanticConventions.AttributeHttpStatusCode
                Assert.True(string.IsNullOrEmpty(span.GetStatus().Description));
            }
            else
            {
                Assert.Equal(Status.Unset, span.GetStatus());

                // Instrumentation is not expected to set status description
                // as the reason can be inferred from SemanticConventions.AttributeHttpStatusCode
                Assert.True(string.IsNullOrEmpty(span.GetStatus().Description));
            }

            var expectedUri = new Uri(expectedUrl);
            var actualUrl = span.GetTagValue(SemanticConventions.AttributeHttpUrl);

            Assert.Equal(expectedUri.ToString(), actualUrl);

            // Url strips 80 or 443 if the scheme matches.
            if ((expectedUri.Port == 80 && expectedUri.Scheme == "http") || (expectedUri.Port == 443 && expectedUri.Scheme == "https"))
            {
                Assert.DoesNotContain($":{expectedUri.Port}", actualUrl as string);
            }
            else
            {
                Assert.Contains($":{expectedUri.Port}", actualUrl as string);
            }

            // Host includes port if it isn't 80 or 443.
            if (expectedUri.Port == 80 || expectedUri.Port == 443)
            {
                Assert.Equal(
                    expectedUri.Host,
                    span.GetTagValue(SemanticConventions.AttributeHttpHost) as string);
            }
            else
            {
                Assert.Equal(
                    $"{expectedUri.Host}:{expectedUri.Port}",
                    span.GetTagValue(SemanticConventions.AttributeHttpHost) as string);
            }

            Assert.Equal(HttpContext.Current.Request.HttpMethod, span.GetTagValue(SemanticConventions.AttributeHttpMethod) as string);
            Assert.Equal(HttpContext.Current.Request.Path, span.GetTagValue(SpanAttributeConstants.HttpPathKey) as string);
            Assert.Equal(HttpContext.Current.Request.UserAgent, span.GetTagValue(SemanticConventions.AttributeHttpUserAgent) as string);
        }

        [Theory]
        [InlineData(SamplingDecision.Drop)]
        [InlineData(SamplingDecision.RecordOnly)]
        [InlineData(SamplingDecision.RecordAndSample)]

        public void ExtractContextIrrespectiveOfSamplingDecision(SamplingDecision samplingDecision)
        {
            HttpContext.Current = new HttpContext(
                new HttpRequest(string.Empty, "http://localhost/", string.Empty)
                {
                    RequestContext = new RequestContext()
                    {
                        RouteData = new RouteData(),
                    },
                },
                new HttpResponse(new StringWriter()));

            bool isPropagatorCalled = false;
            var propagator = new Mock<TextMapPropagator>();
            propagator.Setup(m => m.Extract(It.IsAny<PropagationContext>(), It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>()))
                .Returns(() =>
                {
                    isPropagatorCalled = true;
                    return default(PropagationContext);
                });

            var activity = new Activity(HttpInListener.ActivityOperationName);

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            Sdk.SetDefaultTextMapPropagator(propagator.Object);
            using (var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new TestSampler(samplingDecision))
                .AddAspNetInstrumentation()
                .AddProcessor(activityProcessor.Object).Build())
            {
                activity.Start();

                using (var inMemoryEventListener = new InMemoryEventListener(AspNetInstrumentationEventSource.Log))
                {
                    this.fakeAspNetDiagnosticSource.Write("Start", null);
                }

                this.fakeAspNetDiagnosticSource.Write("Stop", null);
                activity.Stop();
            }

            Assert.True(isPropagatorCalled);
        }

        [Fact]
        public void ExtractContextIrrespectiveOfTheFilterApplied()
        {
            HttpContext.Current = new HttpContext(
                new HttpRequest(string.Empty, "http://localhost/", string.Empty)
                {
                    RequestContext = new RequestContext()
                    {
                        RouteData = new RouteData(),
                    },
                },
                new HttpResponse(new StringWriter()));

            bool isPropagatorCalled = false;
            var propagator = new Mock<TextMapPropagator>();
            propagator.Setup(m => m.Extract(It.IsAny<PropagationContext>(), It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>()))
                .Returns(() =>
                {
                    isPropagatorCalled = true;
                    return default(PropagationContext);
                });

            var activity = new Activity(HttpInListener.ActivityOperationName);

            bool isFilterCalled = false;
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            Sdk.SetDefaultTextMapPropagator(propagator.Object);
            using (var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddAspNetInstrumentation(options =>
                {
                    options.Filter = context =>
                    {
                        isFilterCalled = true;
                        return false;
                    };
                })
                .AddProcessor(activityProcessor.Object).Build())
            {
                activity.Start();

                using (var inMemoryEventListener = new InMemoryEventListener(AspNetInstrumentationEventSource.Log))
                {
                    this.fakeAspNetDiagnosticSource.Write("Start", null);
                }

                this.fakeAspNetDiagnosticSource.Write("Stop", null);
                activity.Stop();
            }

            Assert.True(isFilterCalled);
            Assert.True(isPropagatorCalled);
        }

        private static Action<Activity, string, object> GetEnrichmentAction(Status statusToBeSet)
        {
            Action<Activity, string, object> enrichAction;

            enrichAction = (activity, method, obj) =>
            {
                Assert.True(activity.IsAllDataRequested);
                switch (method)
                {
                    case "OnStartActivity":
                        Assert.True(obj is HttpRequest);
                        break;

                    case "OnStopActivity":
                        Assert.True(obj is HttpResponse);
                        if (statusToBeSet != default)
                        {
                            activity.SetStatus(statusToBeSet);
                        }

                        break;

                    default:
                        break;
                }
            };

            return enrichAction;
        }

        private class FakeAspNetDiagnosticSource : IDisposable
        {
            private readonly DiagnosticListener listener;

            public FakeAspNetDiagnosticSource()
            {
                this.listener = new DiagnosticListener(AspNetInstrumentation.AspNetDiagnosticListenerName);
            }

            public void Write(string name, object value)
            {
                this.listener.Write(name, value);
            }

            public void Dispose()
            {
                this.listener.Dispose();
            }
        }

        private class TestSampler : Sampler
        {
            private SamplingDecision samplingDecision;

            public TestSampler(SamplingDecision samplingDecision)
            {
                this.samplingDecision = samplingDecision;
            }

            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                return new SamplingResult(this.samplingDecision);
            }
        }
    }
}
