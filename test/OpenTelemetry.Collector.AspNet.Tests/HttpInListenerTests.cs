// <copyright file="HttpInListenerTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Routing;
using Moq;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Collector.AspNet.Tests
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
        [InlineData("http://localhost/", 0, null)]
        [InlineData("https://localhost/", 0, null)]
        [InlineData("http://localhost:443/", 0, null)] // Test http over 443
        [InlineData("https://localhost:80/", 0, null)] // Test https over 80
        [InlineData("http://localhost:80/Index", 1, "{controller}/{action}/{id}")]
        [InlineData("https://localhost:443/about_attr_route/10", 2, "about_attr_route/{customerId}")]
        [InlineData("http://localhost:1880/api/weatherforecast", 3, "api/{controller}/{id}")]
        [InlineData("https://localhost:1843/subroute/10", 4, "subroute/{customerId}")]
        [InlineData("http://localhost/api/value", 0, null, "/api/value")] // Request will be filtered
        [InlineData("http://localhost/api/value/2", 0, null, "/api/value")] // Request will not be filtered
        [InlineData("http://localhost/api/value", 0, null, "{ThrowException}")] // Filter user code will throw an exception
        public void AspNetRequestsAreCollectedSuccessfully(string url, int routeType, string routeTemplate, string filter = null)
        {
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

            var activity = new Activity("Current").AddBaggage("Stuff", "123");
            activity.Start();
            try
            {
                var spanProcessor = new Mock<SpanProcessor>();
                var tracer = TracerFactory.Create(b => b
                        .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                    .GetTracer(null);

                using (new AspNetCollector(
                    tracer,
                    new AspNetCollectorOptions
                    {
                        RequestFilter = httpContext =>
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
                        },
                    }))
                {
                    this.fakeAspNetDiagnosticSource.Write(
                        "Start",
                        null);

                    this.fakeAspNetDiagnosticSource.Write(
                        "Stop",
                        null);
                }

                if (HttpContext.Current.Request.Path == filter || filter == "{ThrowException}")
                {
                    Assert.Equal(0, spanProcessor.Invocations.Count); // Nothing was called because request was filtered.
                    return;
                }

                Assert.Equal(2, spanProcessor.Invocations.Count);

                var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

                Assert.Equal(routeTemplate ?? HttpContext.Current.Request.Path, span.Name);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(CanonicalCode.Ok, span.Status.CanonicalCode);
                Assert.Equal("OK", span.Status.Description);

                var expectedUri = new Uri(url);
                var actualUrl = (string)span.Attributes.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpUrlKey).Value;

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
                        span.Attributes.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpHostKey).Value as string);
                }
                else
                {
                    Assert.Equal(
                        $"{expectedUri.Host}:{expectedUri.Port}",
                        span.Attributes.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpHostKey).Value as string);
                }

                Assert.Equal(
                    HttpContext.Current.Request.HttpMethod,
                    span.Attributes.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpMethodKey).Value as string);
                Assert.Equal(
                    HttpContext.Current.Request.Path,
                    span.Attributes.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value as string);
                Assert.Equal(
                    HttpContext.Current.Request.UserAgent,
                    span.Attributes.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpUserAgentKey).Value as string);
            }
            finally
            {
                activity.Stop();
            }
        }

        private class FakeAspNetDiagnosticSource : IDisposable
        {
            private readonly DiagnosticListener listener;

            public FakeAspNetDiagnosticSource()
            {
                this.listener = new DiagnosticListener(AspNetCollector.AspNetDiagnosticListenerName);
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
