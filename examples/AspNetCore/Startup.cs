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
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.AspNetCore
{
    public class Startup
    {
        private MeterProvider meterProvider;

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

            // Switch between Zipkin/Jaeger by setting UseExporter in appsettings.json.
            var exporter = this.Configuration.GetValue<string>("UseExporter").ToLowerInvariant();
            switch (exporter)
            {
                case "jaeger":
                    services.AddOpenTelemetryTracing((builder) => builder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(this.Configuration.GetValue<string>("Jaeger:ServiceName")))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddJaegerExporter());

                    services.Configure<JaegerExporterOptions>(this.Configuration.GetSection("Jaeger"));
                    break;
                case "zipkin":
                    services.AddOpenTelemetryTracing((builder) => builder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(this.Configuration.GetValue<string>("Zipkin:ServiceName")))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddZipkinExporter());

                    services.Configure<ZipkinExporterOptions>(this.Configuration.GetSection("Zipkin"));
                    break;
                case "otlp":
                    // Adding the OtlpExporter creates a GrpcChannel.
                    // This switch must be set before creating a GrpcChannel/HttpClient when calling an insecure gRPC service.
                    // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                    services.AddOpenTelemetryTracing((builder) => builder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(this.Configuration.GetValue<string>("Otlp:ServiceName")))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(this.Configuration.GetValue<string>("Otlp:Endpoint"));
                        }));
                    break;
                default:
                    services.AddOpenTelemetryTracing((builder) => builder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddConsoleExporter());

                    // For options which can be bound from IConfiguration.
                    services.Configure<AspNetCoreInstrumentationOptions>(this.Configuration.GetSection("AspNetCoreInstrumentation"));

                    // For options which can be configured from code only.
                    services.Configure<AspNetCoreInstrumentationOptions>(options =>
                    {
                        options.Filter = (req) =>
                        {
                            return req.Request.Host != null;
                        };
                    });

                    break;
            }

            // TODO: Add IServiceCollection.AddOpenTelemetryMetrics extension method
            var providerBuilder = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation();

            // TODO: Add configuration switch for Prometheus and OTLP export
            providerBuilder
                .AddConsoleExporter();

            this.meterProvider = providerBuilder.Build();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

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
