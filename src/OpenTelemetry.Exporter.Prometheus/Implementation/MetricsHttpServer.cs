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

namespace OpenTelemetry.Exporter.Prometheus.Implementation
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using OpenTelemetry.Stats;

    internal class MetricsHttpServer
    {
        private readonly IViewManager viewManager;

        private readonly CancellationToken token;

        private readonly HttpListener httpListener = new HttpListener();

        public MetricsHttpServer(IViewManager viewManager, PrometheusExporterOptions options, CancellationToken token)
        {
            this.viewManager = viewManager;
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
                            foreach (var view in this.viewManager.AllExportedViews)
                            {
                                var data = this.viewManager.GetView(view.Name);

                                var builder = new PrometheusMetricBuilder()
                                    .WithName(data.View.Name.AsString)
                                    .WithDescription(data.View.Description);

                                builder = data.View.Aggregation.Match<PrometheusMetricBuilder>(
                                    (agg) => { return builder.WithType("gauge"); }, // Func<ISum, M> p0
                                    (agg) => { return builder.WithType("counter"); }, // Func< ICount, M > p1,
                                    (agg) => { return builder.WithType("histogram"); }, // Func<IMean, M> p2,
                                    (agg) => { return builder.WithType("histogram"); }, // Func< IDistribution, M > p3,
                                    (agg) => { return builder.WithType("gauge"); }, // Func<ILastValue, M> p4,
                                    (agg) => { return builder.WithType("gauge"); }); // Func< IAggregation, M > p6);

                                foreach (var value in data.AggregationMap)
                                {
                                    var metricValueBuilder = builder.AddValue();

                                    // TODO: This is not optimal. Need to refactor to split builder into separate functions
                                    metricValueBuilder = value.Value.Match<PrometheusMetricBuilder.PrometheusMetricValueBuilder>(
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue,
                                        metricValueBuilder.WithValue);

                                    for (var i = 0; i < value.Key.Values.Count; i++)
                                    {
                                        metricValueBuilder.WithLabel(data.View.Columns[i].Name, value.Key.Values[i].AsString);
                                    }
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
