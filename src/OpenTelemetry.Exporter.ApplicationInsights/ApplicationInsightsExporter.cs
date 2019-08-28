// <copyright file="ApplicationInsightsExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.ApplicationInsights
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using OpenTelemetry.Exporter.ApplicationInsights.Implementation;
    using OpenTelemetry.Stats;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Trace.Export;

    /// <summary>
    /// Exporter of OpenTelemetry spans and metrics to Azure Application Insights.
    /// </summary>
    public class ApplicationInsightsExporter
    {
        private const string TraceExporterName = "ApplicationInsightsTraceExporter";

        private readonly TelemetryConfiguration telemetryConfiguration;

        private readonly TelemetryClient telemetryClient;

        private readonly IViewManager viewManager;

        private readonly ISpanExporter exporter;

        private readonly TimeSpan exportMetricsInterval;

        private readonly object lck = new object();

        private TraceExporterHandler handler;

        private CancellationTokenSource tokenSource;

        private Task exportMetricsTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInsightsExporter"/> class.
        /// This exporter allows to send OpenTelemetry data to Azure Application Insights.
        /// </summary>
        /// <param name="exporter">Exporter to get traces and metrics from.</param>
        /// <param name="viewManager">View manager to get stats from.</param>
        /// <param name="telemetryConfiguration">Telemetry configuration to use to report telemetry.</param>
        /// <param name="exportMetricsInterval">The export metrics interval. Defaults to 1 minute.</param>
        public ApplicationInsightsExporter(
            ISpanExporter exporter,
            IViewManager viewManager,
            TelemetryConfiguration telemetryConfiguration,
            TimeSpan? exportMetricsInterval = null)
        {
            this.exporter = exporter;
            this.viewManager = viewManager;
            this.telemetryConfiguration = telemetryConfiguration;
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
            this.exportMetricsInterval = exportMetricsInterval ?? TimeSpan.FromMinutes(1);
        }

        /// <summary>
        /// Start exporter.
        /// </summary>
        public void Start()
        {
            lock (this.lck)
            {
                if (this.handler != null)
                {
                    return;
                }

                this.handler = new TraceExporterHandler(this.telemetryConfiguration);

                this.exporter.RegisterHandler(TraceExporterName, this.handler);

                this.tokenSource = new CancellationTokenSource();

                this.exportMetricsTask = this.ExportMetricsAsync(this.exportMetricsInterval, this.tokenSource.Token);
            }
        }

        /// <summary>
        /// Stop exporter.
        /// </summary>
        public void Stop()
        {
            lock (this.lck)
            {
                if (this.handler == null)
                {
                    return;
                }

                this.exporter.UnregisterHandler(TraceExporterName);
                this.tokenSource.Cancel();
                this.exportMetricsTask.Wait();
                this.tokenSource = null;

                this.handler = null;
            }
        }

        /// <summary>
        /// Starts the metrics export task.
        /// </summary>
        /// <param name="exportInterval">The export interval.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A Task that when complete signals the end of the continous Export.</returns>
        private async Task ExportMetricsAsync(TimeSpan exportInterval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(exportInterval, cancellationToken).ConfigureAwait(false);

                    try
                    {
                        this.Export();
                    }
                    catch (Exception ex)
                    {
                        // TODO Log to something useful.
                        Debug.WriteLine(ex);
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Exports this instance.
        /// </summary>
        private void Export()
        {
            foreach (var view in this.viewManager.AllExportedViews)
            {
                var data = this.viewManager.GetView(view.Name);

                foreach (var value in data.AggregationMap)
                {
                    var metricTelemetry = new MetricTelemetry
                    {
                        Name = data.View.Name.AsString,
                    };

                    for (var i = 0; i < value.Key.Values.Count; i++)
                    {
                        var name = data.View.Columns[i].Name;
                        var val = value.Key.Values[i].AsString;
                        metricTelemetry.Properties.Add(name, val);
                    }

                    // Now those properties needs to be populated.
                    //
                    // metricTelemetry.Sum
                    // metricTelemetry.Count
                    // metricTelemetry.Max
                    // metricTelemetry.Min
                    // metricTelemetry.StandardDeviation
                    //
                    // See data model for clarification on the meaning of those fields.
                    // https://docs.microsoft.com/azure/application-insights/application-insights-data-model-metric-telemetry

                    value.Value.Match<object>(
                        (combined) =>
                        {
                            if (combined is ISumDataDouble sum)
                            {
                                metricTelemetry.Sum = sum.Sum;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is ISumDataLong sum)
                            {
                                metricTelemetry.Sum = sum.Sum;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is ICountData count)
                            {
                                metricTelemetry.Sum = count.Count;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is IMeanData mean)
                            {
                                metricTelemetry.Sum = mean.Mean * mean.Count;
                                metricTelemetry.Count = (int)mean.Count;
                                metricTelemetry.Max = mean.Max;
                                metricTelemetry.Min = mean.Min;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is IDistributionData dist)
                            {
                                metricTelemetry.Sum = dist.Mean * dist.Count;
                                metricTelemetry.Count = (int)dist.Count;
                                metricTelemetry.Min = dist.Min;
                                metricTelemetry.Max = dist.Max;
                                metricTelemetry.StandardDeviation = dist.SumOfSquaredDeviations;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is ILastValueDataDouble lastValue)
                            {
                                metricTelemetry.Sum = lastValue.LastValue;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is ILastValueDataLong lastValue)
                            {
                                metricTelemetry.Sum = lastValue.LastValue;
                            }

                            return null;
                        },
                        (combined) =>
                        {
                            if (combined is IAggregationData aggregationData)
                            {
                                // TODO: report an error
                            }

                            return null;
                        });

                    this.telemetryClient.TrackMetric(metricTelemetry);
                }
            }
        }
    }
}
