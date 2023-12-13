// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var instrumentationOptions = new InstrumentationOptions();

builder.Configuration.GetSection("InstrumentationOptions").Bind(instrumentationOptions);

Console.WriteLine("EnableOTelInstrumentation: " + instrumentationOptions.EnableInstrumentation);
Console.WriteLine("EnableMiddlewareInstrumentation: " + instrumentationOptions.EnableMiddlewareInstrumentation);

// Configure OpenTelemetry with tracing and auto-start.
if (instrumentationOptions.EnableInstrumentation)
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation());
}

if (instrumentationOptions.EnableMiddlewareInstrumentation)
{
    builder.Services.AddOpenTelemetry()
       .WithTracing(tracing => tracing
       .AddSource("Microsoft.AspNetCore"));

    builder.Services.AddSingleton(new TelemetryMiddleware(new AspNetCoreTraceInstrumentationOptions()));
}

builder.Logging.ClearProviders();
var app = builder.Build();

if (instrumentationOptions.EnableMiddlewareInstrumentation)
{
    app.UseMiddleware<TelemetryMiddleware>();
}

app.MapGet("/", () =>
{
    return $"Hello World! OpenTelemetry Trace: {Activity.Current?.Id}";
});

app.Run();
