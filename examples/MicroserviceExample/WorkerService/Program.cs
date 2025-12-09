// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Utils.Messaging;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MessageReceiver>();

builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddSource(nameof(MessageReceiver))
        .AddOtlpExporter(o =>
        {
            var otelCollectorHostName = Environment.GetEnvironmentVariable("OTEL_COLLECTOR_HOSTNAME") ?? "localhost";
            o.Endpoint = new Uri($"http://{otelCollectorHostName}:4318/v1/traces");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

var app = builder.Build();

app.Run();
