// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.GrpcService;

internal sealed class Startup
{
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .ConfigureResource(r => r.AddService(this.Configuration.GetValue("ServiceName", defaultValue: "otel-test")))
                    .AddAspNetCoreInstrumentation();

                // Switch between Otlp/Zipkin/Console by setting UseExporter in appsettings.json.
                var exporter = this.Configuration.GetValue("UseExporter", defaultValue: "console").ToUpperInvariant();
                switch (exporter)
                {
                    case "OTLP":
                        builder.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(this.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317"));
                        });
                        break;
                    case "ZIPKIN":
                        builder.AddZipkinExporter(zipkinOptions =>
                        {
                            zipkinOptions.Endpoint = new Uri(this.Configuration.GetValue("Zipkin:Endpoint", defaultValue: "http://localhost:9411/api/v2/spans"));
                        });
                        break;
                    default:
                        builder.AddConsoleExporter();
                        break;
                }
            });
    }
}
