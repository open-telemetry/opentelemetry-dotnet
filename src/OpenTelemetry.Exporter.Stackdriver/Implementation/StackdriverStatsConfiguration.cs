// <copyright file="StackdriverStatsConfiguration.cs" company="OpenTelemetry Authors">
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
using Google.Api;
using Google.Apis.Auth.OAuth2;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    /// <summary>
    /// Configuration for exporting stats into Stackdriver.
    /// </summary>
    public class StackdriverStatsConfiguration
    {
        private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets default Stats Configuration for Stackdriver.
        /// </summary>
        public static StackdriverStatsConfiguration Default
        {
            get
            {
                var defaultConfig = new StackdriverStatsConfiguration
                {
                    ExportInterval = DefaultInterval,
                    ProjectId = GoogleCloudResourceUtils.GetProjectId(),
                    MetricNamePrefix = string.Empty,
                };

                defaultConfig.MonitoredResource = GoogleCloudResourceUtils.GetDefaultResource(defaultConfig.ProjectId);
                return defaultConfig;
            }
        }

        /// <summary>
        /// Gets or sets frequency of the export operation.
        /// </summary>
        public TimeSpan ExportInterval { get; set; }

        /// <summary>
        /// Gets or sets the prefix to append to every OpenTelemetry metric name in Stackdriver.
        /// </summary>
        public string MetricNamePrefix { get; set; }

        /// <summary>
        /// Gets or sets google Cloud Project Id.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Gets or sets credential used to authenticate against Google Stackdriver Monitoring APIs.
        /// </summary>
        public GoogleCredential GoogleCredential { get; set; }

        /// <summary>
        /// Gets or sets monitored Resource associated with metrics collection.
        /// By default, the exporter detects the environment where the export is happening,
        /// such as GKE/AWS/GCE. If the exporter is running on a different environment,
        /// monitored resource will be identified as "general".
        /// </summary>
        public MonitoredResource MonitoredResource { get; set; }
    }
}
