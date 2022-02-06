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
using Examples.AspNet6;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// OpenTelemetry
var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(TracingConstants.ActivitySourceName, serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName.ToString());

builder.Logging.ClearProviders();

builder.Logging.AddConsole();

builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder)
        .AddConsoleExporter()
        .AddOtlpExporter();
});

builder.Services.AddOpenTelemetryTracing(options =>
{
    options.SetResourceBuilder(resourceBuilder)
       .AddSource(TracingConstants.ActivitySourceName)
       .AddHttpClientInstrumentation()
       .AddConsoleExporter()
       .AddOtlpExporter();
});

builder.Services.AddOpenTelemetryMetrics(options =>
{
    options.SetResourceBuilder(resourceBuilder)
        .AddHttpClientInstrumentation()
        .AddMeter(AspNet6Meter.MeterName)
        .AddConsoleExporter()
        .AddOtlpExporter();
});

builder.Services.AddSingleton<AspNet6Meter>();

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
