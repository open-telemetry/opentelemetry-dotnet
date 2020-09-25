// <copyright file="LocalServer.cs" company="OpenTelemetry Authors">
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

#if NETCOREAPP

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Helper
{
    public class LocalServer : IDisposable
    {
        private readonly IWebHost host;
        private TracerProvider tracerProvider;

        public LocalServer(string url, bool enableTracerProvider = false)
        {
            void ConfigureTestServices(IServiceCollection services)
            {
                if (enableTracerProvider)
                {
                    this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddAspNetCoreInstrumentation()
                        .Build();
                }
            }

            this.host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .UseUrls(url)
                .ConfigureServices(configure => ConfigureTestServices(configure))
                .Build();

            Task.Run(() => this.host.Run());
        }

        public void Dispose()
        {
            try
            {
                this.tracerProvider.Dispose();
                this.host.Dispose();
            }
            catch (Exception)
            {
                // ignored, see https://github.com/aspnet/KestrelHttpServer/issues/1513
                // Kestrel 2.0.0 should have fix it, but it does not seem important for our tests
            }
        }

        private class Startup
        {
            public void Configure(IApplicationBuilder app)
            {
                app.Run(async (context) =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            }
        }
    }
}
#endif
