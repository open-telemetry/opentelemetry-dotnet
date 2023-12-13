// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
