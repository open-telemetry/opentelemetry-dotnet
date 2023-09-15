// <copyright file="Startup.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.GrpcService;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .ConfigureResource(r => r.AddService(this.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!))
                    .AddAspNetCoreInstrumentation();

                // Switch between Otlp/Zipkin/Console by setting UseExporter in appsettings.json.
                var exporter = this.Configuration.GetValue("UseExporter", defaultValue: "console")!.ToLowerInvariant();
                switch (exporter)
                {
                    case "otlp":
                        builder.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(this.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
                        });
                        break;
                    case "zipkin":
                        builder.AddZipkinExporter(zipkinOptions =>
                        {
                            zipkinOptions.Endpoint = new Uri(this.Configuration.GetValue("Zipkin:Endpoint", defaultValue: "http://localhost:9411/api/v2/spans")!);
                        });
                        break;
                    default:
                        builder.AddConsoleExporter();
                        break;
                }
            });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<GreeterService>();

            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909").ConfigureAwait(false);
            });
        });
    }
}
