// <copyright file="PrometheusAspNetCoreOptions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using OpenTelemetry.Exporter.Prometheus;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Prometheus exporter options.
    /// </summary>
    public class PrometheusAspNetCoreOptions
    {
        internal const string DefaultScrapeEndpointPath = "/metrics";

        /// <summary>
        /// Gets or sets the path to use for the scraping endpoint. Default value: "/metrics".
        /// </summary>
        public string ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;

        /// <summary>
        /// Gets or sets the cache duration in milliseconds for scrape responses. Default value: 300.
        /// </summary>
        /// <remarks>
        /// Note: Specify 0 to disable response caching.
        /// </remarks>
        public int ScrapeResponseCacheDurationMilliseconds
        {
            get => this.ExporterOptions.ScrapeResponseCacheDurationMilliseconds;
            set => this.ExporterOptions.ScrapeResponseCacheDurationMilliseconds = value;
        }

        internal PrometheusExporterOptions ExporterOptions { get; } = new();
    }
}
