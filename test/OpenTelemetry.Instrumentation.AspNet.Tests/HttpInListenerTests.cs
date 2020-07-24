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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests
{
    public class HttpInListenerTests : IDisposable
    {
        private static readonly string ActivityNameAspNet = "Microsoft.AspNet.HttpReqIn.Start";
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
        [InlineData("http://localhost/", 0, null, "TraceContext")]
        [InlineData("https://localhost/", 0, null, "TraceContext")]
        [InlineData("http://localhost:443/", 0, null, "TraceContext")] // Test http over 443
        [InlineData("https://localhost:80/", 0, null, "TraceContext")] // Test https over 80
        [InlineData("http://localhost:80/Index", 1, "{controller}/{action}/{id}", "TraceContext")]
        [InlineData("https://localhost:443/about_attr_route/10", 2, "about_attr_route/{customerId}", "TraceContext")]
        [InlineData("http://localhost:1880/api/weatherforecast", 3, "api/{controller}/{id}", "TraceContext")]
        [InlineData("https://localhost:1843/subroute/10", 4, "subroute/{customerId}", "TraceContext")]

        // TODO: Reenable this tests once filtering mechanism is designed.
        // [InlineData("http://localhost/api/value", 0, null, "/api/value")] // Request will be filtered
        // [InlineData("http://localhost/api/value", 0, null, "{ThrowException}")] // Filter user code will throw an exception
        [InlineData("http://localhost/api/value/2", 0, null, "CustomContext", "/api/value")] // Request will not be filtered
        [InlineData("http://localhost/api/value/2", 0, null, "CustomContext", "/api/value", true)] // Request will not be filtered
        public void AspNetRequestsAreCollectedSuccessfully(
            string url,
            int routeType,
            string routeTemplate,
            string carrierFormat,
            string filter = null,
            bool restoreCurrentActivity = false)
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var s = carrierFormat;
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
            var textFormat = new Mock<ITextFormatActivity>();
            textFormat.Setup(m => m.Extract<HttpRequest>(It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>())).Returns(new ActivityContext(
                expectedTraceId,
                expectedSpanId,
                ActivityTraceFlags.Recorded));

            var activity = new Activity(ActivityNameAspNet).AddBaggage("Stuff", "123");
            activity.SetParentId(expectedTraceId, expectedSpanId, ActivityTraceFlags.Recorded);
            var activityProcessor = new Mock<ActivityProcessor>();
            using (openTelemetry = OpenTelemetrySdk.CreateTracerProvider(
            (builder) => builder.AddAspNetInstrumentation(
                (options) =>
                {
                    options.RequestFilter = httpContext =>
                    {
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

                    if (!carrierFormat.Equals("TraceContext"))
                    {
                        options.TextFormat = textFormat.Object;
                    }
                })
            .SetResource(expectedResource)
            .AddProcessorPipeline(p => p.AddProcessor(_ => activityProcessor.Object))))
            {
                activity.Start();
                this.fakeAspNetDiagnosticSource.Write(
                    "Start",
                    null);

                if (restoreCurrentActivity)
                {
                    Activity.Current = activity;
                }

                this.fakeAspNetDiagnosticSource.Write(
                    "Stop",
                    null);

                // The above line fires DS event which is listened by Instrumentation.
                // Validate that Current activity is still the one created by Asp.Net
                Assert.Equal(ActivityNameAspNet, Activity.Current.OperationName);
                activity.Stop();
            }

            if (HttpContext.Current.Request.Path == filter || filter == "{ThrowException}")
            {
                Assert.Equal(0, activityProcessor.Invocations.Count); // Nothing was called because request was filtered.
                return;
            }

            // Validate that Activity.Current is always the one created by Asp.Net
            var currentActivity = Activity.Current;

            Activity span;
            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            span = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(routeTemplate ?? HttpContext.Current.Request.Path, span.DisplayName);
            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.True(span.Duration != TimeSpan.Zero);

            Assert.Equal(
                "200",
                span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpStatusCode).Value);

            Assert.Equal(
                "Ok",
                span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);

            Assert.Equal(
                "OK",
                span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusDescriptionKey).Value);

            var expectedUri = new Uri(url);
            var actualUrl = span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpUrl).Value;

            Assert.Equal(expectedUri.ToString(), actualUrl);

            // Url strips 80 or 443 if the scheme matches.
            if ((expectedUri.Port == 80 && expectedUri.Scheme == "http") || (expectedUri.Port == 443 && expectedUri.Scheme == "https"))
            {
                Assert.DoesNotContain($":{expectedUri.Port}", actualUrl);
            }
            else
            {
                Assert.Contains($":{expectedUri.Port}", actualUrl);
            }

            // Host includes port if it isn't 80 or 443.
            if (expectedUri.Port == 80 || expectedUri.Port == 443)
            {
                Assert.Equal(
                    expectedUri.Host,
                    span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpHost).Value as string);
            }
            else
            {
                Assert.Equal(
                    $"{expectedUri.Host}:{expectedUri.Port}",
                    span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpHost).Value as string);
            }

            Assert.Equal(
                HttpContext.Current.Request.HttpMethod,
                span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpMethod).Value as string);
            Assert.Equal(
                HttpContext.Current.Request.Path,
                span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value as string);
            Assert.Equal(
                HttpContext.Current.Request.UserAgent,
                span.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeHttpUserAgent).Value as string);

            Assert.Equal(expectedResource, span.GetResource());
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
    }
}
