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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Extensibility;
    using OpenTelemetry.Exporter.ApplicationInsights.Implementation;
    using OpenTelemetry.Stats;
    using OpenTelemetry.Trace.Export;

    /// <summary>
    /// Exporter of Open Census traces and metrics to Azure Application Insights.
    /// </summary>
    public class ApplicationInsightsExporter
    {
        private const string TraceExporterName = "ApplicationInsightsTraceExporter";

        private readonly TelemetryConfiguration telemetryConfiguration;

        private readonly IViewManager viewManager;

        private readonly ISpanExporter exporter;

        private readonly object lck = new object();

        private TraceExporterHandler handler;

        private CancellationTokenSource tokenSource;

        private Task workerThread;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInsightsExporter"/> class.
        /// This exporter allows to send Open Census data to Azure Application Insights.
        /// </summary>
        /// <param name="exporter">Exporter to get traces and metrics from.</param>
        /// <param name="viewManager">View manager to get stats from.</param>
        /// <param name="telemetryConfiguration">Telemetry configuration to use to report telemetry.</param>
        public ApplicationInsightsExporter(ISpanExporter exporter, IViewManager viewManager, TelemetryConfiguration telemetryConfiguration)
        {
            this.exporter = exporter;
            this.viewManager = viewManager;
            this.telemetryConfiguration = telemetryConfiguration;
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

                var token = this.tokenSource.Token;

                var metricsExporter = new MetricsExporterThread(this.telemetryConfiguration, this.viewManager, token, TimeSpan.FromMinutes(1));
                this.workerThread = Task.Factory.StartNew((Action)metricsExporter.WorkerThread, TaskCreationOptions.LongRunning);
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
                this.workerThread.Wait();
                this.tokenSource = null;

                this.handler = null;
            }
        }
    }
}
