// <copyright file="ConsoleExporterOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter
{
    public class ConsoleExporterOptions
    {
        /// <summary>
        /// Gets or sets the output targets for the console exporter.
        /// </summary>
        public ConsoleExporterOutputTargets Targets { get; set; } = ConsoleExporterOutputTargets.Console;

        /// <summary>
        /// Gets or sets the metric export interval in milliseconds. The default value is 1000 milliseconds.
        /// </summary>
        public int MetricExportIntervalMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to export Delta
        /// values or not (Cumulative).
        /// </summary>
        public bool IsDelta { get; set; } = false;
    }
}
