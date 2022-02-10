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
using Examples.AspNetCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "AspNetCoreExampleService";

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// OpenTelemetry
var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(serviceName, serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName);

builder.Logging.ClearProviders();

builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder);
    var metricsExporter = builder.Configuration.GetValue<string>("UseLogExporter").ToLowerInvariant();
    switch (metricsExporter)
    {
        case "otlp":
            options.AddOtlpExporter();
            break;
        default:
            options.AddConsoleExporter();
            break;
    }
});

builder.Services.AddOpenTelemetryTracing(options =>
{
    options
        .SetResourceBuilder(resourceBuilder)
        .SetSampler(new AlwaysOnSampler())
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddSource(Tracing.ActivitySourceName);

    // Switch between Zipkin/Jaeger/OTLP by setting UseExporter in appsettings.json.
    var tracingExporter = builder.Configuration.GetValue<string>("UseTracingExporter").ToLowerInvariant();
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


builder.Services.AddOpenTelemetryMetrics(options =>
{
    options.SetResourceBuilder(resourceBuilder)
        .AddHttpClientInstrumentation()
        .AddMeter(ExampleMeter.MeterName);

    var metricsExporter = builder.Configuration.GetValue<string>("UseMetricsExporter").ToLowerInvariant();
    switch (metricsExporter)
    {
        case "prometheus":
            options.AddPrometheusExporter();
            break;
        case "otlp":
            options.AddOtlpExporter();
            break;
        default:
            options.AddConsoleExporter(options =>
            {
                // The ConsoleMetricExporter defaults to a manual collect cycle.
                // This configuration causes metrics to be exported to stdout on a 10s interval.
                options.MetricReaderType = MetricReaderType.Periodic;
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000;
            });
            break;
    }
});

builder.Services.AddSingleton<ExampleMeter>();

builder.Services.Configure<AspNetCoreInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

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
