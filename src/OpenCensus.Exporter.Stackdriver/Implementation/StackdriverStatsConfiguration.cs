// <copyright file="ICountData.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Exporter.Stackdriver.Implementation
{
    using Google.Api;
    using Google.Apis.Auth.OAuth2;
    using System;

    /// <summary>
    /// Configuration for exporting stats into Stackdriver
    /// </summary>
    public class StackdriverStatsConfiguration
    {
        private static readonly TimeSpan DEFAULT_INTERVAL = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Frequency of the export operation
        /// </summary>
        public TimeSpan ExportInterval { get; set; }

        /// <summary>
        /// The prefix to append to every OpenCensus metric name in Stackdriver
        /// </summary>
        public string MetricNamePrefix { get; set; }

        /// <summary>
        /// Google Cloud Project Id
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Credential used to authenticate against Google Stackdriver Monitoring APIs
        /// </summary>
        public GoogleCredential GoogleCredential { get; set; }

        /// <summary>
        /// Monitored Resource associated with metrics collection.
        /// By default, the exporter detects the environment where the export is happening,
        /// such as GKE/AWS/GCE. If the exporter is running on a different environment,
        /// monitored resource will be identified as "general".
        /// </summary>
        public MonitoredResource MonitoredResource { get; set; }

        /// <summary>
        /// Default Stats Configuration for Stackdriver
        /// </summary>
        public static StackdriverStatsConfiguration Default
        {
            get
            {
                var defaultConfig = new StackdriverStatsConfiguration
                {
                    ExportInterval = DEFAULT_INTERVAL,
                    ProjectId = GoogleCloudResourceUtils.GetProjectId(),
                    MetricNamePrefix = string.Empty,
                };

                defaultConfig.MonitoredResource = GoogleCloudResourceUtils.GetDefaultResource(defaultConfig.ProjectId);
                return defaultConfig;
            }
        }
    }
}