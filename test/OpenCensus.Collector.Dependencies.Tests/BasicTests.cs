// <copyright file="BasicTests.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Collector.Dependencies.Tests
{
    using Moq;
    using OpenCensus.Common;
    using OpenCensus.Trace;
    using OpenCensus.Trace.Config;
    using OpenCensus.Trace.Internal;
    using OpenCensus.Trace.Propagation;
    using OpenCensus.Trace.Sampler;
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Xunit;

    public partial class HttpClientTests
    {
        [Fact]
        public async Task HttpDepenenciesCollectorInjectsHeadersAsync()
        {
            var startEndHandler = new Mock<IStartEndHandler>();

            var serverLifeTime = TestServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Close();
                },
                out string host,
                out int port);

            var url = $"http://{host}:{port}/";

            ITraceId expectedTraceId = TraceId.Invalid;
            ISpanId expectedSpanId = SpanId.Invalid;

            using (serverLifeTime)
            {
                var tracer = new Tracer(new RandomGenerator(), startEndHandler.Object, new TraceConfig());

                var tf = new Mock<ITextFormat>();
                tf
                    .Setup(m => m.Inject<HttpRequestMessage>(It.IsAny<ISpanContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                    .Callback((ISpanContext sc, HttpRequestMessage obj, Action<HttpRequestMessage, string, string> setter) =>
                    {
                        expectedTraceId = sc.TraceId;
                        expectedSpanId = sc.SpanId;
                    });

                var propagationComponent = new Mock<IPropagationComponent>();
                propagationComponent.SetupGet(m => m.TextFormat).Returns(tf.Object);

                using (var dc = new DependenciesCollector(new DependenciesCollectorOptions(), tracer, Samplers.AlwaysSample, propagationComponent.Object))
                {

                    using (var c = new HttpClient())
                    {
                        var request = new HttpRequestMessage
                        {
                            RequestUri = new Uri(url),
                            Method = new HttpMethod("GET"),
                        };

                        await c.SendAsync(request);
                    }
                }
            }

            Assert.Equal(2, startEndHandler.Invocations.Count); // begin and end was called
            var spanData = ((Span)startEndHandler.Invocations[1].Arguments[0]).ToSpanData();

            Assert.Equal(expectedTraceId, spanData.Context.TraceId);
            Assert.Equal(expectedSpanId, spanData.Context.SpanId);
        }
    }
}
