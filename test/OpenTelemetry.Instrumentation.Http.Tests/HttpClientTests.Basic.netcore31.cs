// <copyright file="HttpClientTests.Basic.netcore31.cs" company="OpenTelemetry Authors">
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
#if NETCOREAPP3_1
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
{
    public partial class HttpClientTests : IDisposable
    {
        private readonly IDisposable serverLifeTime;
        private readonly string url;

        public HttpClientTests()
        {
            this.serverLifeTime = TestHttpServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            this.url = $"http://{host}:{port}/";
        }

        [Fact]
        public void AddHttpClientInstrumentation_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddHttpClientInstrumentation());
        }

        [Fact]
        public async Task HttpClientInstrumentationInjectsHeadersAsync()
        {
            var processor = new Mock<ActivityProcessor>();
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            // Ensure that the header value func does not throw if the header key can't be found
            var mockTextFormat = new Mock<ITextFormat>();

            // var isInjectedHeaderValueGetterThrows = false;
            // mockTextFormat
            //     .Setup(x => x.IsInjected(It.IsAny<HttpRequestMessage>(), It.IsAny<Func<HttpRequestMessage, string, IEnumerable<string>>>()))
            //     .Callback<HttpRequestMessage, Func<HttpRequestMessage, string, IEnumerable<string>>>(
            //         (carrier, getter) =>
            //         {
            //             try
            //             {
            //                 // traceparent doesn't exist
            //                 getter(carrier, "traceparent");
            //             }
            //             catch
            //             {
            //                 isInjectedHeaderValueGetterThrows = true;
            //             }
            //         });

            using (Sdk.CreateTracerProviderBuilder()
                        .AddHttpClientInstrumentation(o => o.TextFormat = mockTextFormat.Object)
                        .AddProcessor(processor.Object)
                        .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(3, processor.Invocations.Count); // start/end/dispose was called
            var activity = (Activity)processor.Invocations[1].Arguments[0];

            ValidateHttpClientActivity(activity, true);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00-{activity.Context.TraceId}-{activity.Context.SpanId}-01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
        }

        [Fact]
        public async Task HttpClientInstrumentationInjectsHeadersAsync_CustomFormat()
        {
            var textFormat = new Mock<ITextFormat>();
            textFormat.Setup(m => m.Inject<HttpRequestMessage>(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                {
                    action(message, "custom_traceparent", $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
                    action(message, "custom_tracestate", Activity.Current.TraceStateString);
                });

            var processor = new Mock<ActivityProcessor>();

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation((opt) => opt.TextFormat = textFormat.Object)
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(3, processor.Invocations.Count); // start/end/dispose was called
            var activity = (Activity)processor.Invocations[1].Arguments[0];

            ValidateHttpClientActivity(activity, true);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("custom_traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("custom_tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00/{activity.Context.TraceId}/{activity.Context.SpanId}/01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
        }

        [Fact]
        public async Task HttpClientInstrumentation_AddViaFactory_HttpInstrumentation_CollectsSpans()
        {
            var processor = new Mock<ActivityProcessor>();

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Single(processor.Invocations.Where(i => i.Method.Name == "OnStart"));
            Assert.Single(processor.Invocations.Where(i => i.Method.Name == "OnEnd"));
            Assert.IsType<Activity>(processor.Invocations[1].Arguments[0]);
        }

        [Fact]
        public async Task HttpClientInstrumentation_AddViaFactory_DependencyInstrumentation_CollectsSpans()
        {
            var processor = new Mock<ActivityProcessor>();

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Single(processor.Invocations.Where(i => i.Method.Name == "OnStart"));
            Assert.Single(processor.Invocations.Where(i => i.Method.Name == "OnEnd"));
            Assert.IsType<Activity>(processor.Invocations[1].Arguments[0]);
        }

        [Fact]
        public async Task HttpClientInstrumentationBacksOffIfAlreadyInstrumented()
        {
            var processor = new Mock<ActivityProcessor>();

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(1, processor.Invocations.Count); // dispose
        }

        [Fact]
        public async void HttpClientInstrumentationFiltersOutRequests()
        {
            var processor = new Mock<ActivityProcessor>();
            using (Sdk.CreateTracerProviderBuilder()
                               .AddHttpClientInstrumentation(
                        (opt) => opt.FilterFunc = (req) => !req.RequestUri.OriginalString.Contains(this.url))
                               .AddProcessor(processor.Object)
                               .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Equal(1, processor.Invocations.Count);  // dispose
        }

        [Fact]
        public async Task HttpClientInstrumentationFiltersOutRequestsToExporterEndpoints()
        {
            var processor = new Mock<ActivityProcessor>();

            using (Sdk.CreateTracerProviderBuilder()
                               .AddHttpClientInstrumentation()
                               .AddProcessor(processor.Object)
                               .Build())
            {
                using var c = new HttpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                try
                {
                    await c.PostAsync("https://dc.services.visualstudio.com/", new StringContent(string.Empty), cts.Token);
                    await c.PostAsync("https://localhost:9411/api/v2/spans", new StringContent(string.Empty), cts.Token);
                }
                catch
                {
                    // ignore all, whatever response is, we don't want anything tracked
                }
            }

            Assert.Equal(1, processor.Invocations.Count);  // dispose
        }

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
            Activity.Current = null;
        }

        private static void ValidateHttpClientActivity(Activity activityToValidate, bool responseExpected)
        {
            Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
            var request = activityToValidate.GetCustomProperty(HttpHandlerDiagnosticListener.RequestCustomPropertyName);
            Assert.NotNull(request);
            Assert.True(request is HttpRequestMessage);

            if (responseExpected)
            {
                var response = activityToValidate.GetCustomProperty(HttpHandlerDiagnosticListener.ResponseCustomPropertyName);
                Assert.NotNull(response);
                Assert.True(response is HttpResponseMessage);
            }
        }
    }
}
#endif
