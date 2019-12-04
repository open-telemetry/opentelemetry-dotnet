// <copyright file="MetricsHttpServer.cs" company="OpenTelemetry Authors">
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
using System;
using System.IO;
using System.Net;
using System.Threading;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Exporter.Prometheus.Implementation
{
    internal class MetricsHttpServer<T>
    {
        private readonly Metric<T> metric;

        private readonly CancellationToken token;

        private readonly HttpListener httpListener = new HttpListener();

        public MetricsHttpServer(Metric<T> metric, PrometheusExporterOptions options, CancellationToken token)
        {
            this.metric = metric;
            this.token = token;
            this.httpListener.Prefixes.Add(options.Url);
        }

        public void WorkerThread()
        {
            this.httpListener.Start();

            try
            {
                while (!this.token.IsCancellationRequested)
                {
                    var ctxTask = this.httpListener.GetContextAsync();
                    ctxTask.Wait(this.token);

                    var ctx = ctxTask.Result;

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = PrometheusMetricBuilder.ContentType;

                    using (var output = ctx.Response.OutputStream)
                    {
                        using (var writer = new StreamWriter(output))
                        {
                            foreach (var metricSeries in this.metric.TimeSeries)
                            {
                                var labels = metricSeries.Key.Labels;
                                var values = metricSeries.Value.Points;

                                var builder = new PrometheusMetricBuilder()
                                    .WithName(this.metric.MetricName)
                                    .WithDescription(this.metric.MetricDescription);

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
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // this will happen when cancellation will be requested
            }
            catch (Exception)
            {
                // TODO: report error
            }
            finally
            {
                this.httpListener.Stop();
                this.httpListener.Close();
            }
        }
    }
}
