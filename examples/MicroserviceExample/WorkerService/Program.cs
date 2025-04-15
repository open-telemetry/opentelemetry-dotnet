// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Utils.Messaging;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MessageReceiver>();

builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddSource(nameof(MessageReceiver))
        .AddZipkinExporter(o =>
        {
            var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
            o.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
        }));

var app = builder.Build();

app.Run();
