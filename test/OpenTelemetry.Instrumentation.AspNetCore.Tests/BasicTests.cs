﻿// <copyright file="BasicTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Configuration;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Moq;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace.Samplers;
using TestApp.AspNetCore._3._1;
using System.Linq;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
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
        public void AddRequestInstrumentation_BadArgs()
        {
            OpenTelemetryBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddRequestInstrumentation());
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallGeneratesASpan()
        {
            var spanProcessor = new Mock<ActivityProcessor>();

            void ConfigureTestServices(IServiceCollection services)
            {
                var openTelemetry = OpenTelemetrySdk.Default.EnableOpenTelemetry(
                (builder) => builder.AddRequestInstrumentation()
                .SetProcessorPipeline((p => p.AddProcessor(n => spanProcessor.Object))));

                /*
                services.AddSingleton<TracerFactory>(_ =>
                    TracerFactory.Create(b => b
                        .SetSampler(new AlwaysOnSampler())
                        .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))
                        .AddRequestInstrumentation()));
                */
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
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.Equal("/api/values", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);
        }

        [Fact]
        public async Task SuccessfulTemplateControllerCallUsesParentContext()
        {
            var spanProcessor = new Mock<ActivityProcessor>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        OpenTelemetrySdk.Default.EnableOpenTelemetry(
                        (builder) => builder.AddRequestInstrumentation()
                        .SetProcessorPipeline((p => p.AddProcessor(n => spanProcessor.Object))));

                        /*
                        services.AddSingleton<TracerFactory>(_ =>
                            TracerFactory.Create(b => b
                                .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))
                                .AddRequestInstrumentation())); */
                    })))
            {
                using var client = testFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/values/2");
                request.Headers.Add("traceparent", $"00-{expectedTraceId}-{expectedSpanId}-01");

                // Act
                var response = await client.SendAsync(request);

                // Assert
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(spanProcessor, 2);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.Equal("api/Values/{id}", span.DisplayName);
            Assert.Equal("/api/values/2", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);

            Assert.Equal(expectedTraceId, span.Context.TraceId);
            Assert.Equal(expectedSpanId, span.ParentSpanId);
        }

        [Fact(Skip = "TODO: Reenable once custom format support is added")]
        public async Task CustomTextFormat()
        {
            var spanProcessor = new Mock<ActivityProcessor>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var textFormat = new Mock<ITextFormat>();
            textFormat.Setup(m => m.Extract<HttpRequest>(It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>())).Returns(new SpanContext(
                expectedTraceId,
                expectedSpanId,
                ActivityTraceFlags.Recorded,
                true));

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        OpenTelemetrySdk.Default.EnableOpenTelemetry(
                        (builder) => builder.AddRequestInstrumentation()
                        .SetProcessorPipeline((p => p.AddProcessor(n => spanProcessor.Object))));

                        /*
                        services.AddSingleton<TracerFactory>(_ =>
                            TracerFactory.Create(b => b
                                .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))
                                .AddRequestInstrumentation(o => o.TextFormat = textFormat.Object)));*/
                    })))
            {
                using var client = testFactory.CreateClient();
                var response = await client.GetAsync("/api/values/2");
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForProcessorInvocations(spanProcessor, 2);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Server, span.Kind);
            Assert.Equal("api/Values/{id}", span.DisplayName);
            Assert.Equal("/api/values/2", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.HttpPathKey).Value);

            Assert.Equal(expectedTraceId, span.Context.TraceId);
            Assert.Equal(expectedSpanId, span.ParentSpanId);
        }


        [Fact(Skip = "TODO: Reenable once filtering is fixed")]
        public async Task FilterOutRequest()
        {
            var spanProcessor = new Mock<ActivityProcessor>();

            void ConfigureTestServices(IServiceCollection services)
            {
                var openTelemetry = OpenTelemetrySdk.Default.EnableOpenTelemetry(
                (builder) => builder.AddRequestInstrumentation()
                .SetProcessorPipeline((p => p.AddProcessor(n => spanProcessor.Object))));

                /*services.AddSingleton<TracerFactory>(_ =>
                    TracerFactory.Create(b => b
                        .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))
                        .AddRequestInstrumentation(o => o.RequestFilter = (httpContext) => httpContext.Request.Path != "/api/values/2")));
                */
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

                WaitForProcessorInvocations(spanProcessor, 2);
            }

            // we should only create one span and never call processor with another
            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal("/api/values", span.Attributes.GetValue("http.path"));
        }

        private static void WaitForProcessorInvocations(Mock<ActivityProcessor> spanProcessor, int invocationCount)
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
