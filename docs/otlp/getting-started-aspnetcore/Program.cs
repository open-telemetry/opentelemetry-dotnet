// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Clear default log providers added by the host.
builder.Logging.ClearProviders();

// Configure OpenTelemetry with logs, metrics, & tracing and auto-start.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation())
    .UseOtlpExporter();

// Set OpenTelemetry metrics to use delta temporality.
builder.Services.Configure<MetricReaderOptions>(
    o => o.TemporalityPreference = MetricReaderTemporalityPreference.Delta);

var app = builder.Build();

app.MapGet("/", () => $"Hello World! OpenTelemetry Trace: {Activity.Current?.Id}");

app.Run();
