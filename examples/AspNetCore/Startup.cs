// <copyright file="Startup.cs" company="OpenTelemetry Authors">
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

using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.AspNetCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });

            services.AddOpenTelemetryTracing((builder) => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "aspnetcore", serviceNamespace: "example"))
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter()
                .AddZipkinExporter(options =>
                {
                    var hostname = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
                    options.Endpoint = new Uri($"http://{hostname}:9411/api/v2/spans");
                })
                .AddOtlpExporter(options =>
                {
                    var hostname = Environment.GetEnvironmentVariable("OTLP_HOSTNAME") ?? "localhost";
                    options.Endpoint = new Uri($"http://{hostname}:4317");
                })
                .AddJaegerExporter(options =>
                {
                    var hostname = Environment.GetEnvironmentVariable("JAEGER_HOSTNAME") ?? "localhost";
                    options.AgentHost = hostname;
                    options.Endpoint = new Uri($"http://{hostname}:14268");
                }));

            // For options which can be bound from IConfiguration.
            services.Configure<AspNetCoreInstrumentationOptions>(this.Configuration.GetSection("AspNetCoreInstrumentation"));

            // For options which can be configured from code only.
            services.Configure<AspNetCoreInstrumentationOptions>(options =>
            {
                options.Filter = (req) =>
                {
                    return req.Request.Host.HasValue;
                };
            });

            services.AddOpenTelemetryMetrics(builder => builder
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter(options =>
                {
                    // The ConsoleMetricExporter defaults to a manual collect cycle.
                    // This configuration causes metrics to be exported to stdout on a 10s interval.
                    options.MetricReaderType = MetricReaderType.Periodic;
                    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000;
                })
                .AddOtlpExporter(options =>
                {
                    var hostname = Environment.GetEnvironmentVariable("OTLP_HOSTNAME") ?? "localhost";
                    options.Endpoint = new Uri($"http://{hostname}:4317");
                }));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
