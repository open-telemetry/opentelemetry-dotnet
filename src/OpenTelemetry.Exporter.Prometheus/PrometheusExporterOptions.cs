// <copyright file="PrometheusExporterOptions.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// <see cref="PrometheusExporter"/> options.
    /// </summary>
    public class PrometheusExporterOptions
    {
        internal const string DefaultScrapeEndpointPath = "/metrics";
        internal Func<DateTimeOffset> GetUtcNowDateTimeOffset = () => DateTimeOffset.UtcNow;

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether or not an http listener
        /// should be started. Default value: False.
        /// </summary>
        public bool StartHttpListener { get; set; }
#else
        /// <summary>
        /// Gets or sets a value indicating whether or not an http listener
        /// should be started. Default value: True.
        /// </summary>
        public bool StartHttpListener { get; set; } = true;
#endif

        /// <summary>
        /// Gets or sets the prefixes to use for the http listener. Default
        /// value: http://*:80/.
        /// </summary>
        public IReadOnlyCollection<string> HttpListenerPrefixes { get; set; } = new string[] { "http://*:80/" };

        /// <summary>
        /// Gets or sets the path to use for the scraping endpoint. Default value: /metrics.
        /// </summary>
        public string ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;
    }
}
