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
    serviceName, serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName);

// Traces
builder.Services.AddOpenTelemetryTracing(options =>
{
    options
        .ConfigureResource(configureResource)
        .SetSampler(new AlwaysOnSampler())
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation();

    switch (tracingExporter)
    {
        case "jaeger":
            options.AddJaegerExporter();

            builder.Services.Configure<JaegerExporterOptions>(builder.Configuration.GetSection("Jaeger"));

            // Customize the HttpClient that will be used when JaegerExporter is configured for HTTP transport.
            builder.Services.AddHttpClient("JaegerExporter", configureClient: (client) => client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value"));
            break;

        case "zipkin":
            options.AddZipkinExporter();

            builder.Services.Configure<ZipkinExporterOptions>(builder.Configuration.GetSection("Zipkin"));
            break;

        case "otlp":
            options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint"));
                });
            break;

        default:
            options.AddConsoleExporter();

            break;
    }
});

// For options which can be bound from IConfiguration.
builder.Services.Configure<AspNetCoreInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

// Logging
builder.Logging.ClearProviders();

// Note: See appsettings.json Logging:OpenTelemetry section for OpenTelemetryLoggerOptions bindings.
var loggerBuilder = builder.Logging.AddOpenTelemetry();

loggerBuilder.ConfigureResource(configureResource);

// Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
var logExporter = builder.Configuration.GetValue<string>("UseLogExporter").ToLowerInvariant();
switch (logExporter)
{
    case "otlp":
        loggerBuilder.AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint"));
        });
        break;
    default:
        loggerBuilder.AddConsoleExporter();
        break;
}

builder.Services.Configure<OpenTelemetryLoggerOptions>(opt =>
{
    opt.IncludeScopes = true;
    opt.ParseStateValues = true;
    opt.IncludeFormattedMessage = true;
});

// Metrics
// Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
var metricsExporter = builder.Configuration.GetValue<string>("UseMetricsExporter").ToLowerInvariant();

builder.Services.AddOpenTelemetryMetrics(options =>
{
    options.ConfigureResource(configureResource)
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation();

    switch (metricsExporter)
    {
        case "prometheus":
            options.AddPrometheusExporter();
            break;
        case "otlp":
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint"));
            });
            break;
        default:
            options.AddConsoleExporter();
            break;
    }
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

if (metricsExporter.Equals("prometheus", StringComparison.OrdinalIgnoreCase))
{
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
}

app.Run();
