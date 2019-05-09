
// <copyright file="StackdriverExporter.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Exporter.Stackdriver
{
    using Google.Api.Gax;
    using Google.Apis.Auth.OAuth2;
    using Google.Cloud.Monitoring.V3;
    using OpenCensus.Exporter.Stackdriver.Implementation;
    using OpenCensus.Stats;
    using OpenCensus.Trace.Export;

    /// <summary>
    /// Implementation of the exporter to Stackdriver
    /// </summary>
    public class StackdriverExporter
    {
        private const string ExporterName = "StackdriverTraceExporter";

        private readonly IExportComponent exportComponent;
        private readonly IViewManager viewManager;
        private readonly string projectId;
        private readonly string jsonPath;
        private StackdriverStatsExporter statsExporter;
        private object locker = new object();
        private bool isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="StackdriverExporter"/> class.
        /// </summary>
        /// <param name="projectId">Google Cloud ProjectId that is used to send data to Stackdriver</param>
        /// <param name="exportComponent">Exporter to get traces from</param>
        /// <param name="viewManager">View manager to get the stats from</param>
        public StackdriverExporter(
            string projectId,
            IExportComponent exportComponent,
            IViewManager viewManager) : this(projectId, null, exportComponent, viewManager)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StackdriverExporter"/> class.
        /// </summary>
        /// <param name="projectId">Google Cloud ProjectId that is used to send data to Stackdriver</param>
        /// <param name="jsonPath">File path to the json file containing the service credential used to authenticate against Stackdriver APIs</param>
        /// <param name="exportComponent">Exporter to get traces from</param>
        /// <param name="viewManager">View manager to get the stats from</param>
        public StackdriverExporter(
            string projectId,
            string jsonPath,
            IExportComponent exportComponent,
            IViewManager viewManager)
        {
            GaxPreconditions.CheckNotNullOrEmpty(projectId, "projectId");

            this.projectId = projectId;
            this.jsonPath = jsonPath;
            this.exportComponent = exportComponent;
            this.viewManager = viewManager;
        }

        /// <summary>
        /// Starts the exporter
        /// </summary>
        public void Start()
        {
            lock (locker)
            {
                if (isInitialized)
                {
                    return;
                }

                // Register trace exporter
                if (exportComponent != null)
                {
                    var traceExporter = new StackdriverTraceExporter(projectId);
                    exportComponent.SpanExporter.RegisterHandler(ExporterName, traceExporter);
                }

                // Register stats(metrics) exporter
                if (viewManager != null)
                {
                    GoogleCredential credential = GetGoogleCredential();

                    StackdriverStatsConfiguration statsConfig = StackdriverStatsConfiguration.Default;
                    statsConfig.GoogleCredential = credential;
                    if (statsConfig.ProjectId != projectId)
                    {
                        statsConfig.ProjectId = projectId;
                        statsConfig.MonitoredResource = GoogleCloudResourceUtils.GetDefaultResource(projectId);
                    }

                    statsExporter = new StackdriverStatsExporter(viewManager, statsConfig);
                    statsExporter.Start();
                }

                isInitialized = true;
            }
        }

        /// <summary>
        /// Stops the exporter
        /// </summary>
        public void Stop()
        {
            lock (locker)
            {
                if (!isInitialized)
                {
                    return;
                }

                // Stop tracing exporter
                if (exportComponent != null)
                {
                    exportComponent.SpanExporter.UnregisterHandler(ExporterName);
                }

                // Stop metrics exporter
                if (statsExporter != null)
                {
                    statsExporter.Stop();
                }

                isInitialized = false;
            }
        }

        private GoogleCredential GetGoogleCredential()
        {
            GoogleCredential credential;
            if (string.IsNullOrEmpty(jsonPath))
            {
                credential = GoogleCredential.GetApplicationDefault();
            }
            else
            {
                credential = GoogleCredential.FromFile(jsonPath)
                 .CreateScoped(MetricServiceClient.DefaultScopes);
            }

            return credential;
        }
    }
}