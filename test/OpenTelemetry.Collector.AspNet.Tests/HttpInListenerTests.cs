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
        [InlineData("https://localhost:443/", 0, null)]
        [InlineData("https://localhost:443/Index", 1, "{controller}/{action}/{id}")]
        [InlineData("https://localhost:443/about_attr_route/10", 2, "about_attr_route/{customerId}")]
        [InlineData("https://localhost:443/api/weatherforecast", 3, "api/{controller}/{id}")]
        [InlineData("https://localhost:443/subroute/10", 4, "subroute/{customerId}")]
        public void AspNetRequestsAreCollectedSuccessfully(string url, int routeType, string routeTemplate)
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
                    routeData.Values.Add("MS_SubRoutes", new[] {
                        new
                        {
                            Route = new
                            {
                                RouteTemplate = routeTemplate,
                            },
                        },
                    });
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
                new HttpRequest("", url, "")
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

            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            using (new AspNetCollector(tracer))
            {
                this.fakeAspNetDiagnosticSource.Write(
                    "Start",
                    null);

                this.fakeAspNetDiagnosticSource.Write(
                    "Stop",
                    null);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin was called

            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(routeTemplate ?? HttpContext.Current.Request.Path, span.Name);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(CanonicalCode.Ok, span.Status.CanonicalCode);
            Assert.Equal("OK", span.Status.Description);

            Assert.Equal(HttpContext.Current.Request.Url.Host, span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.HttpHostKey).Value as string);
            Assert.Equal(HttpContext.Current.Request.HttpMethod, span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.HttpMethodKey).Value as string);
            Assert.Equal(HttpContext.Current.Request.Path, span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.HttpPathKey).Value as string);
            Assert.Equal(HttpContext.Current.Request.UserAgent, span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.HttpUserAgentKey).Value as string);
            Assert.Equal(HttpContext.Current.Request.Url.ToString(), span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.HttpUrlKey).Value as string);

            activity.Stop();
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
