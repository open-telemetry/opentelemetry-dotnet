// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Utils.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<MessageSender>();

builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddSource(nameof(MessageSender))
        .AddZipkinExporter(o =>
        {
            var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
            o.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
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
