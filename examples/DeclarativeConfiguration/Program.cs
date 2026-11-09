// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

using var activitySource = new ActivitySource("Examples.DeclarativeConfiguration");

var builder = Host.CreateApplicationBuilder(args);

// The only OpenTelemetry code. The document turns tracing on and defines the pipeline.
builder.Services.AddOpenTelemetry().UseDeclarativeConfiguration("otel-config.yaml");

using var host = builder.Build();
await host.StartAsync();

using (var activity = activitySource.StartActivity("SayHello"))
{
    activity?.SetTag("greeting", "Hello from a declarative configuration file");
}

await host.StopAsync();
