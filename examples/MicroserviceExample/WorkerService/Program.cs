// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Utils.Messaging;

namespace WorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();

                    services.AddSingleton<MessageReceiver>();

                    services.AddOpenTelemetryTracing((builder) => builder
                        .AddAspNetCoreInstrumentation()
                        .AddOtlpExporter(opt =>
                        {
                            opt.Endpoint = "ingest.lightstep.com:443";
                            opt.Headers = new Metadata
                            {
                                { "lightstep-access-token", Environment.GetEnvironmentVariable("LS_ACCESS_TOKEN")}
                            };
                            opt.Credentials = new SslCredentials();
                        })
                        .SetResource(
                            Resources.CreateServiceResource(Environment.GetEnvironmentVariable("LS_SERVICE_NAME"),
                            serviceVersion: Environment.GetEnvironmentVariable("LS_SERVICE_VERSION")))
                    );
                });
    }
}
