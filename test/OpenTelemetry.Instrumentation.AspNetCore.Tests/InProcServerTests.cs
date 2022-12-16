// <copyright file="InProcServerTests.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public sealed class InProcServerTests : IDisposable
    {
        private TracerProvider tracerProvider;
        private WebApplication app;
        private HttpClient client;
        private List<Activity> exportedItems;

        public InProcServerTests()
        {
            this.exportedItems = new List<Activity>();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            var app = builder.Build();

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(this.exportedItems).Build();
            app.MapGet("/", () => "Hello World!");
            app.RunAsync();

            this.app = app;
            this.client = new HttpClient();
        }

        [Fact]
        public async void ExampleTest()
        {
            var res = await this.client.GetStringAsync("http://localhost:5000").ConfigureAwait(false);
            Assert.NotNull(res);

            this.tracerProvider.ForceFlush();
            for (var i = 0; i < 10; i++)
            {
                if (this.exportedItems.Count > 0)
                {
                    break;
                }

                // We need to let End callback execute as it is executed AFTER response was returned.
                // In unit tests environment there may be a lot of parallel unit tests executed, so
                // giving some breezing room for the End callback to complete
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }

            var activity = this.exportedItems[0];
            Assert.Equal(ActivityKind.Server, activity.Kind);
            Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeNetHostName));
            Assert.Equal(5000, activity.GetTagValue(SemanticConventions.AttributeNetHostPort));
            Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeHttpMethod));
            Assert.Equal("1.1", activity.GetTagValue(SemanticConventions.AttributeHttpFlavor));
            Assert.Equal(200, activity.GetTagValue(SemanticConventions.AttributeHttpStatusCode));
            Assert.True(activity.Status == ActivityStatusCode.Unset);
            Assert.True(activity.StatusDescription is null);
        }

        public async void Dispose()
        {
            this.tracerProvider.Dispose();
            this.client.Dispose();
            await this.app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
#endif
