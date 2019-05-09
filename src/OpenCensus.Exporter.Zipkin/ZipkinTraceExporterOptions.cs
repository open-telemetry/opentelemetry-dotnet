// <copyright file="ZipkinTraceExporterOptions.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Exporter.Zipkin
{
    using System;

    /// <summary>
    /// Zipkin trace exporter options.
    /// </summary>
    public sealed class ZipkinTraceExporterOptions
    {
        /// <summary>
        /// Gets or sets Zipkin endpoint address. See https://zipkin.io/zipkin-api/#/default/post_spans.
        /// Typically https://zipkin-server-name:9411/api/v2/spans.
        /// </summary>
        public Uri Endpoint { get; set; } = new Uri("http://localhost:9411/api/v2/spans");

        /// <summary>
        /// Gets or sets timeout in seconds.
        /// </summary>
        public TimeSpan TimeoutSeconds { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the name of the service reporting telemetry.
        /// </summary>
        public string ServiceName { get; set; } = "Open Census Exporter";

        /// <summary>
        /// Gets or sets a value indicating whether short trace id should be used.
        /// </summary>
        public bool UseShortTraceIds { get; set; } = false;
    }
}
