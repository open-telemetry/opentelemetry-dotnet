// <copyright file="WebHostBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

// #if NETSTANDARD

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Extensions for configuring Prometheus with <see cref="IWebHostBuilder"/>.
    /// </summary>
    public static class WebHostBuilderExtensions
    {
        /// <summary>
        /// Configures Prometheus with the provided <see cref="IWebHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="options">The <see cref="PrometheusExporterOptions"/> to configure the exporter with.</param>
        /// <param name="metrics">The <see cref="List{Metric}"/> to export.</param>
        /// <returns>The <see cref="IWebHostBuilder"/> for chaining.</returns>
        public static IWebHostBuilder UsePrometheus(this IWebHostBuilder builder, PrometheusExporterOptions options, List<Metric> metrics)
        {
            return builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton(new PrometheusExporter(options));
            });
        }

        /// <summary>
        /// Configures Prometheus with the provided <see cref="IWebHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebHostBuilder"/> to configure.</param>
        /// <param name="configure">A <see cref="Action{PrometheusExporterOptions}"/> to configure the exporter with.</param>
        /// <param name="metrics">The <see cref="List{Metric}"/> to export.</param>
        /// <returns>The <see cref="IWebHostBuilder"/> for chaining.</returns>
        public static IWebHostBuilder UsePrometheus(this IWebHostBuilder builder, Action<PrometheusExporterOptions> configure, List<Metric> metrics)
        {
            var options = new PrometheusExporterOptions();
            configure(options);

            return builder.UsePrometheus(options, metrics);
        }
    }
}

// #endif
