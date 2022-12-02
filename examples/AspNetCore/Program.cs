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
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var appBuilder = WebApplication.CreateBuilder(args);

// OpenTelemetry
var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

// Switch between Zipkin/Jaeger/OTLP/Console by setting UseTracingExporter in appsettings.json.
var tracingExporter = appBuilder.Configuration.GetValue<string>("UseTracingExporter").ToLowerInvariant();

// Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
var metricsExporter = appBuilder.Configuration.GetValue<string>("UseMetricsExporter").ToLowerInvariant();

// Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
var logExporter = appBuilder.Configuration.GetValue<string>("UseLogExporter").ToLowerInvariant();

// For options which can be bound from IConfiguration.
appBuilder.Services.Configure<AspNetCoreInstrumentationOptions>(appBuilder.Configuration.GetSection("AspNetCoreInstrumentation"));

Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: appBuilder.Configuration.GetValue<string>("ServiceName"),
    serviceVersion: assemblyVersion,
    serviceInstanceId: Environment.MachineName);

appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(configureResource)
    .WithTracing(builder =>
    {
        // Traces

        builder
            .SetSampler(new AlwaysOnSampler())
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        switch (tracingExporter)
        {
            case "jaeger":
                builder.AddJaegerExporter();

                builder.ConfigureServices(services =>
                {
                    services.Configure<JaegerExporterOptions>(appBuilder.Configuration.GetSection("Jaeger"));

                    // Customize the HttpClient that will be used when JaegerExporter is configured for HTTP transport.
                    services.AddHttpClient("JaegerExporter", configureClient: (client) => client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value"));
                });
                break;

            case "zipkin":
                builder.AddZipkinExporter();

                builder.ConfigureServices(services =>
                {
                    services.Configure<ZipkinExporterOptions>(appBuilder.Configuration.GetSection("Zipkin"));
                });
                break;

            case "otlp":
                builder.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue<string>("Otlp:Endpoint"));
                });
                break;

            default:
                builder.AddConsoleExporter();
                break;
        }
    })
    .WithMetrics(builder =>
    {
        // Metrics

        builder
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        switch (metricsExporter)
        {
            case "prometheus":
                builder.AddPrometheusExporter();
                break;
            case "otlp":
                builder.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue<string>("Otlp:Endpoint"));
                });
                break;
            default:
                builder.AddConsoleExporter();
                break;
        }
    })
    .StartWithHost();

// Logging
appBuilder.Logging.ClearProviders();

appBuilder.Logging.AddOpenTelemetry(options =>
{
    // Note: See appsettings.json Logging:OpenTelemetry section for configuration.

    options.ConfigureResource(configureResource);

    switch (logExporter)
    {
        case "otlp":
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue<string>("Otlp:Endpoint"));
            });
            break;
        default:
            options.AddConsoleExporter();
            break;
    }
});

// Add services to the container.
appBuilder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
appBuilder.Services.AddEndpointsApiExplorer();

appBuilder.Services.AddSwaggerGen();

var app = appBuilder.Build();

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
