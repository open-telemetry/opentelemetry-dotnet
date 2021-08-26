// <copyright file="InProcessServer.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
#if NETCOREAPP3_1
using TestApp.AspNetCore._3._1;
#else
using TestApp.AspNetCore._5._0;
#endif
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.W3cTraceContext.Tests
{
    public class InProcessServer : IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly string url;
        private readonly int port;

        private IWebHost hostingEngine;

        public InProcessServer(ITestOutputHelper output)
        {
            this.output = output;
            this.port = 5000;
            this.url = $"http://localhost:{this.port}";
            this.StartServer();
        }

        public void Dispose()
        {
            this.DisposeServer();
        }

        private static string GetTimestamp()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private void StartServer()
        {
            this.output.WriteLine(string.Format("{0}: Starting application at: {1}", GetTimestamp(), this.url));
            var builder = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseUrls(this.url)
                .UseKestrel()
                .UseStartup<Startup>()
                .UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.AddOpenTelemetryTracing((builder) => builder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation());
            });
            this.hostingEngine = builder.Build();
            this.hostingEngine.Start();
        }

        private void DisposeServer()
        {
            if (this.hostingEngine != null)
            {
                this.output.WriteLine(string.Format("{0}: Disposing WebHost starting.....", GetTimestamp()));
                this.hostingEngine.Dispose();
                this.output.WriteLine(string.Format("{0}: Disposing WebHost completed.", GetTimestamp()));
                this.hostingEngine = null;
            }
            else
            {
                this.output.WriteLine(string.Format("{0}: Hosting engine is null.", GetTimestamp()));
            }
        }
    }
}
