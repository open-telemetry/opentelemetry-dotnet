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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Examples.AspNetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();

                    var logExporter = context.Configuration.GetValue<string>("UseLogExporter").ToLowerInvariant();
                    switch (logExporter)
                    {
                        case "otlp":
                            // Adding the OtlpExporter creates a GrpcChannel.
                            // This switch must be set before creating a GrpcChannel when calling an insecure gRPC service.
                            // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                            builder.AddOpenTelemetry(options =>
                            {
                                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(context.Configuration.GetValue<string>("Otlp:ServiceName")));
                                options.AddOtlpExporter(otlpOptions =>
                                 {
                                     otlpOptions.Endpoint = new Uri(context.Configuration.GetValue<string>("Otlp:Endpoint"));
                                 });
                            });
                            break;

                        default:
                            builder.AddOpenTelemetry(options =>
                            {
                                options.AddConsoleExporter();
                            });
                            break;
                    }

                    builder.Services.Configure<OpenTelemetryLoggerOptions>(opt =>
                    {
                        opt.IncludeScopes = true;
                        opt.ParseStateValues = true;
                        opt.IncludeFormattedMessage = true;
                    });
                });
    }
}
