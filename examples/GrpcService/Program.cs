// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Examples.GrpcService;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerBuilder =>
    {
        tracerBuilder.ConfigureResource(r => r.AddService(
            builder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")))
            .AddAspNetCoreInstrumentation();

        var exporter = builder.Configuration.GetValue("UseExporter", defaultValue: "console").ToUpperInvariant();
        switch (exporter)
        {
            case "OTLP":
                tracerBuilder.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317"));
                });
                break;
            case "ZIPKIN":
                tracerBuilder.AddZipkinExporter(zipkinOptions =>
                {
                    zipkinOptions.Endpoint = new Uri(builder.Configuration.GetValue("Zipkin:Endpoint", defaultValue: "http://localhost:9411/api/v2/spans"));
                });
                break;
            default:
                tracerBuilder.AddConsoleExporter();
                break;
        }
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.MapGrpcService<GreeterService>();
app.MapGet("/", async context =>
{
    await context.Response.WriteAsync(
        "Communication with gRPC endpoints must be made through a gRPC client. " +
        "To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909").ConfigureAwait(true);
});

app.Run();
