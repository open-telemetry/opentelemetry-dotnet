// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Utils.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<MessageSender>();

builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddSource(nameof(MessageSender))
        .AddOtlpExporter(o =>
        {
            var otelCollectorHostName = Environment.GetEnvironmentVariable("OTEL_COLLECTOR_HOSTNAME") ?? "localhost";
            o.Endpoint = new Uri($"http://{otelCollectorHostName}:4318/v1/traces");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

builder.WebHost.UseUrls("http://*:5000");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.MapControllers();

app.Run();
