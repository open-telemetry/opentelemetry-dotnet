// <copyright file="ApplicationBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Extensions for configuring Prometheus with <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Configures Promethus with the provided <see cref="IApplicationBuilder"/>.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to configure.</param>
        /// <param name="options">The <see cref="PrometheusExporterOptions"/> to configure the exporter.</param>
        /// <param name="metrics">The <see cref="List{Metric}"/> to export.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
        public static IApplicationBuilder UsePrometheus(this IApplicationBuilder app, PrometheusExporterOptions options, List<Metric> metrics)
        {
            var exporter = new PrometheusExporter(options);
            app.Map(options.Url, a => HandleMetrics(a, exporter, metrics));
            return app;
        }

        /// <summary>
        /// Configures Promethus with the provided <see cref="IApplicationBuilder"/>.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to configure.</param>
        /// <param name="configure">The <see cref="Action{PrometheusExporterOptions}"/> to configure the exporter options.</param>
        /// <param name="metrics">The <see cref="List{Metric}"/> to export.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
        public static IApplicationBuilder UsePrometheus(this IApplicationBuilder app, Action<PrometheusExporterOptions> configure, List<Metric> metrics)
        {
            var options = new PrometheusExporterOptions();
            configure(options);

            return app.UsePrometheus(options, metrics);
        }

        private static void HandleMetrics(IApplicationBuilder app, PrometheusExporter exporter, List<Metric> metrics)
        {
            app.Run(context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = PrometheusMetricBuilder.ContentType;

                using (var writer = new StreamWriter(context.Response.Body))
                {
                    foreach (var metric in metrics)
                    {
                        var labels = metric.Labels;
                        var value = metric.Value;

                        var builder = new PrometheusMetricBuilder()
                            .WithName(metric.MetricName)
                            .WithDescription(metric.MetricDescription);

                        builder = builder.WithType("counter");

                        foreach (var label in labels)
                        {
                            var metricValueBuilder = builder.AddValue();
                            metricValueBuilder = metricValueBuilder.WithValue(value);
                            metricValueBuilder.WithLabel(label.Key, label.Value);
                        }

                        builder.Write(writer);
                    }
                }

                return Task.CompletedTask;
            });
        }
    }
}

// #endif
