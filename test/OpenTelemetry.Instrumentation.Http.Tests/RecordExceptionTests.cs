// <copyright file="RecordExceptionTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
{
    public class RecordExceptionTests : IDisposable
    {
        private readonly IDisposable serverLifeTime;
        private readonly string url;

        public RecordExceptionTests()
        {
            this.serverLifeTime = TestHttpServer.RunServer(
                (ctx) =>
                {
                    if (ctx.Request.Url.PathAndQuery.Contains("500"))
                    {
                        ctx.Response.StatusCode = 500;
                    }
                    else
                    {
                        ctx.Response.StatusCode = 200;
                    }

                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            this.url = $"http://{host}:{port}/";
        }

        [Fact]
        public async Task HttpClientInstrumentationReportsExceptionEventForNetworkFailuresWithClientGetAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri("https://www.invalidurl.com"),
                Method = new HttpMethod("GET"),
            };

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation(o => o.RecordException = true)
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            using var c = new HttpClient();
            try
            {
                await c.SendAsync(request);
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is thrown and collected as event
            Assert.True(exceptionThrown);
            Assert.Single(exportedItems[0].Events.Where(evt => evt.Name.Equals("exception")));
        }

        [Fact]
        public async Task HttpClientInstrumentationDoesNotReportsExceptionEventOnErrorResponseWithClientGetAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation(o => o.RecordException = true)
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            using var c = new HttpClient();
            try
            {
                await c.GetAsync($"{this.url}500");
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is not thrown and not collected as event
            Assert.False(exceptionThrown);
            Assert.Empty(exportedItems[0].Events);
        }

        [Fact]
        public async Task HttpClientInstrumentationReportsDoesNotReportExceptionEventOnErrorResponseWithClientGetStringAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{this.url}500"),
                Method = new HttpMethod("GET"),
            };

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation(o => o.RecordException = true)
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            using var c = new HttpClient();
            try
            {
                await c.GetStringAsync($"{this.url}500");
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is thrown and not collected as event
            Assert.True(exceptionThrown);
            Assert.Empty(exportedItems[0].Events);
        }

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
            Activity.Current = null;
            GC.SuppressFinalize(this);
        }
    }
}
