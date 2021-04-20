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
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
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
                    // Adding OtelTracing. Prepares to accept more configs from DI.
                    services.AddOpenTelemetryTracing();

                    // Configure builder..
                    services.Configure<TracerProviderBuilder>(builder =>
                    {
                        builder.AddHttpClientInstrumentation();
                    });

                    // Configure builder with more..
                    services.Configure<TracerProviderBuilder>(builder =>
                    {
                        builder.AddAspNetCoreInstrumentation();
                    });

                    // and more..
                    services.Configure<TracerProviderBuilder>(builder =>
                    {
                        builder.AddConsoleExporter();
                    });

                    // Configure instrumentation options which can be bound from IConfiguration.
                    // Has no effect unless AddAspNetCoreInstrumentation() is also called.
                    services.Configure<AspNetCoreInstrumentationOptions>(this.Configuration.GetSection("AspNetCoreInstrumentation"));

                    // Configure instrumentation options which can be configured from code only.
                    // Has no effect unless AddAspNetCoreInstrumentation() is also called.
                    services.Configure<AspNetCoreInstrumentationOptions>(options =>
                    {
                        options.Filter = (req) =>
                        {
                            return req.Request.Host != null;
                        };
                    });

                    // Configure builder, but also need to get hold of IServiceProvider.
                    // Ugly??
                    services.Configure<TracerProviderBuilder>(builder =>
                    {
                        builder.Configure((sp, tracerProviderBuilder) =>
                        {
                            // Retrieve something from IServiceProvider.
                            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                            tracerProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddTelemetrySdk());
                        });
                    });

                    break;
            }
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
