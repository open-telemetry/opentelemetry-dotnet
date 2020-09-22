// <copyright file="ZPagesExporterOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// Options to run zpages exporter.
    /// </summary>
    public class ZPagesExporterOptions
    {
        /// <summary>
        /// Gets or sets the port to listen to. Typically it ends with /rpcz like http://localhost:7284/rpcz/.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the retention time (in milliseconds) for the metrics. Default: 100000.
        /// </summary>
        public long RetentionTime { get; set; } = 100000;
    }
}
