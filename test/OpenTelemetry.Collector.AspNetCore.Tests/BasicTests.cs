// <copyright file="BasicTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Sampler;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using TestApp.AspNetCore._2._0;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Moq;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Threading;
using OpenTelemetry.Context.Propagation;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Collector.AspNetCore.Tests
{
    // See https://github.com/aspnet/Docs/tree/master/aspnetcore/test/integration-tests/samples/2.x/IntegrationTestsSample
    public class BasicTests
        : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> factory;

        public BasicTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public void AddRequestCollector_BadArgs()
        {
            TracerBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddRequestCollector());
            Assert.Throws<ArgumentNullException>(() => TracerFactory.Create(b => b.AddRequestCollector(null)));
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            void ConfigureTestServices(IServiceCollection services)
            {
                services.AddSingleton<TracerFactory>(_ =>
                    TracerFactory.Create(b => b
                        .SetSampler(Samplers.AlwaysSample)
                        .SetProcessor(e => spanProcessor.Object)
                        .AddRequestCollector()));
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

                WaitForProcessorInvocations(spanProcessor, 2);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = ((Span)spanProcessor.Invocations[1].Arguments[0]);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal("/api/values", span.Attributes.GetValue("http.path"));
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallUsesParentContext()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var tf = new Mock<ITextFormat>();
            tf.Setup(m => m.Extract<HttpRequest>(It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>())).Returns(new SpanContext(
                expectedTraceId,
                expectedSpanId,
                ActivityTraceFlags.Recorded));

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        services.AddSingleton<TracerFactory>(_ =>
                            TracerFactory.Create(b => b
                                .SetSampler(Samplers.AlwaysSample)
                                .SetTextFormat(tf.Object)
                                .SetProcessor(e => spanProcessor.Object)
                                .AddRequestCollector()));
                    }))
                .CreateClient())
            {

                // Act
                var response = await client.GetAsync("/api/values/2");

                // Assert
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(spanProcessor, 2);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = ((Span)spanProcessor.Invocations[1].Arguments[0]);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal("api/Values/{id}", span.Name);
            Assert.Equal("/api/values/2", span.Attributes.GetValue("http.path"));

            Assert.Equal(expectedTraceId, span.Context.TraceId);
            Assert.Equal(expectedSpanId, span.ParentSpanId);
        }

        [Fact]
        public async Task FilterOutRequest()
        {
            bool Filter(string eventName, object arg1, object _)
            {
                if (eventName == "Microsoft.AspNetCore.Hosting.HttpRequestIn" &&
                    arg1 is HttpContext context &&
                    context.Request.Path == "/api/values/2")
                {
                    return false;
                }

                return true;
            }

            var spanProcessor = new Mock<SpanProcessor>();

            void ConfigureTestServices(IServiceCollection services)
            {
                services.AddSingleton<TracerFactory>(_ =>
                    TracerFactory.Create(b => b
                        .SetSampler(Samplers.AlwaysSample)
                        .SetProcessor(e => spanProcessor.Object)
                        .AddRequestCollector(o => o.EventFilter = Filter)));
            }

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices))
                .CreateClient())
            {

                // Act
                var response1 = await client.GetAsync("/api/values");
                var response2 = await client.GetAsync("/api/values/2");

                // Assert
                response1.EnsureSuccessStatusCode(); // Status Code 200-299
                response2.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(spanProcessor, 2);
            }

            // we should only create one span and never call processor with another
            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = ((Span)spanProcessor.Invocations[1].Arguments[0]);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal("/api/values", span.Attributes.GetValue("http.path"));
        }

        private static void WaitForProcessorInvocations(Mock<SpanProcessor> spanProcessor, int invocationCount)
        {
            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so 
            // giving some breezing room for the End callback to complete
            Assert.True(SpinWait.SpinUntil(() =>
                {
                    Thread.Sleep(10);
                    return spanProcessor.Invocations.Count >= 2;
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
