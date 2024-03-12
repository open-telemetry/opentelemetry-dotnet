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

using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry
var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

// Switch between Zipkin/Jaeger/OTLP/Console by setting UseTracingExporter in appsettings.json.
var tracingExporter = builder.Configuration.GetValue<string>("UseTracingExporter").ToLowerInvariant();

var serviceName = tracingExporter switch
{
    "jaeger" => builder.Configuration.GetValue<string>("Jaeger:ServiceName"),
    "zipkin" => builder.Configuration.GetValue<string>("Zipkin:ServiceName"),
    "otlp" => builder.Configuration.GetValue<string>("Otlp:ServiceName"),
    _ => "AspNetCoreExampleService",
};

Action<ResourceBuilder> configureResource = r => r.AddService(
    "OtlpTest", serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName);

// Traces
builder.Services.AddOpenTelemetry().WithTracing(
        builder =>
        {
            builder.AddHttpClientInstrumentation().AddAspNetCoreInstrumentation();

            builder.AddOtlpExporter();
            builder.AddConsoleExporter();
        });

// For options which can be bound from IConfiguration.
builder.Services.Configure<AspNetCoreInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

// Logging
builder.Logging.ClearProviders();

builder.Logging.AddOpenTelemetry(options =>
{
    // Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
    var logExporter = builder.Configuration.GetValue<string>("UseLogExporter").ToLowerInvariant();
    switch (logExporter)
    {
        case "otlp":
            options.AddOtlpExporter();
            options.AddConsoleExporter();
            break;
        default:
            options.AddConsoleExporter();
            break;
    }
});

builder.Services.Configure<OpenTelemetryLoggerOptions>(opt =>
{
    opt.IncludeScopes = true;
    opt.ParseStateValues = true;
    opt.IncludeFormattedMessage = true;
});

// Metrics
var metricsExporter = builder.Configuration.GetValue<string>("UseMetricsExporter").ToLowerInvariant();

builder.Services.AddOpenTelemetry().WithMetrics(
       builder =>
       {
        builder.ConfigureResource(configureResource)
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation();

        builder.AddMeter("OtlpTest.OtlpTestMeter", "1.0");
        builder.AddOtlpExporter();
        builder.AddConsoleExporter();
    });


// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
