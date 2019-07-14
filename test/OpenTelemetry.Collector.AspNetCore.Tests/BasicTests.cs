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

namespace OpenTelemetry.Collector.AspNetCore.Tests
{
    using Xunit;
    using Microsoft.AspNetCore.Mvc.Testing;
    using TestApp.AspNetCore._2._0;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Internal;
    using Moq;
    using Microsoft.AspNetCore.TestHost;
    using System;
    using OpenTelemetry.Context.Propagation;
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System.Diagnostics;

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
        public async Task SuccesfulTemplateControllerCallGeneratesASpan()
        {
            var startEndHandler = new Mock<IStartEndHandler>();
            var tracer = new Tracer(startEndHandler.Object, new TraceConfig());

            void ConfigureTestServices(IServiceCollection services) =>
                services.AddSingleton<ITracer>(tracer);

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

                for (var i = 0; i < 10; i++)
                {
                    if (startEndHandler.Invocations.Count == 2)
                    {
                        break;
                    }

                    // We need to let End callback execute as it is executed AFTER response was returned.
                    // In unit tests environment there may be a lot of parallel unit tests executed, so 
                    // giving some breezing room for the End callback to complete
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }


            Assert.Equal(2, startEndHandler.Invocations.Count); // begin and end was called
            var spanData = ((Span)startEndHandler.Invocations[1].Arguments[0]).ToSpanData();

            Assert.Equal(SpanKind.Server, spanData.Kind);
            Assert.Equal("/api/values", spanData.Attributes.GetValue("http.path"));
        }

        [Fact]
        public async Task SuccesfulTemplateControllerCallUsesParentContext()
        {
            var startEndHandler = new Mock<IStartEndHandler>();

            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var tf = new Mock<ITextFormat>();
            tf.Setup(m => m.Extract<HttpRequest>(It.IsAny<HttpRequest>(), It.IsAny<Func<HttpRequest, string, IEnumerable<string>>>())).Returns(SpanContext.Create(
                expectedTraceId,
                expectedSpanId,
                ActivityTraceFlags.None,
                Tracestate.Empty
                ));

            var tracer = new Tracer(startEndHandler.Object, new TraceConfig(), null, null, tf.Object);

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices((services) =>
                    {
                        services.AddSingleton<ITracer>(tracer);
                        services.AddSingleton<ITextFormat>(tf.Object);
                        services.AddSingleton<IBinaryFormat>(new BinaryFormat());
                    }))
                .CreateClient())
            {

                // Act
                var response = await client.GetAsync("/api/values/2");

                // Assert
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                for (var i = 0; i < 10; i++)
                {
                    if (startEndHandler.Invocations.Count == 2)
                    {
                        break;
                    }

                    // We need to let End callback execute as it is executed AFTER response was returned.
                    // In unit tests environment there may be a lot of parallel unit tests executed, so 
                    // giving some breezing room for the End callback to complete
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            Assert.Equal(2, startEndHandler.Invocations.Count); // begin and end was called
            var spanData = ((Span)startEndHandler.Invocations[0].Arguments[0]).ToSpanData();

            Assert.Equal(SpanKind.Server, spanData.Kind);
            Assert.Equal("api/Values/{id}", spanData.Name);
            Assert.Equal("/api/values/2", spanData.Attributes.GetValue("http.path"));

            Assert.Equal(expectedTraceId, spanData.Context.TraceId);
            Assert.Equal(expectedSpanId, spanData.ParentSpanId);
        }
    }
}
