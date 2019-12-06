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
        /// <param name="metric">The <see cref="Metric{T}"/> to export.</param>
        /// <typeparam name="T">The type of metric value.</typeparam>
        /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
        public static IApplicationBuilder UsePrometheus<T>(this IApplicationBuilder app, PrometheusExporterOptions options, Metric<T> metric)
            where T : struct
        {
            var exporter = new PrometheusExporter<T>(options, metric);
            app.Map(options.Url, a => HandleMetrics(a, exporter, metric));
            return app;
        }

        /// <summary>
        /// Configures Promethus with the provided <see cref="IApplicationBuilder"/>.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to configure.</param>
        /// <param name="configure">The <see cref="Action{PrometheusExporterOptions}"/> to configure the exporter options.</param>
        /// <param name="metric">The <see cref="Metric{T}"/> to export.</param>
        /// <typeparam name="T">The type of metric value.</typeparam>
        /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
        public static IApplicationBuilder UsePrometheus<T>(this IApplicationBuilder app, Action<PrometheusExporterOptions> configure, Metric<T> metric)
            where T : struct
        {
            var options = new PrometheusExporterOptions();
            configure(options);

            return app.UsePrometheus<T>(options, metric);
        }

        private static void HandleMetrics<T>(IApplicationBuilder app, PrometheusExporter<T> exporter, Metric<T> metric)
            where T : struct
        {
            app.Run(context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = PrometheusMetricBuilder.ContentType;

                using (var writer = new StreamWriter(context.Response.Body))
                {
                    foreach (var metricSeries in metric.TimeSeries)
                    {
                        var labels = metricSeries.Key.Labels;
                        var values = metricSeries.Value.Points;

                        var builder = new PrometheusMetricBuilder()
                            .WithName(metric.MetricName)
                            .WithDescription(metric.MetricDescription);

                        builder = builder.WithType("counter");

                        foreach (var label in labels)
                        {
                            var metricValueBuilder = builder.AddValue();
                            metricValueBuilder = metricValueBuilder.WithValue((long)(object)values[0]);
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
