// <copyright file="OtlpActivityExporter.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// Exporter consuming <see cref="Activity"/> and exporting the data using
    /// the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class OtlpActivityExporter : ActivityExporter
    {
        private readonly TraceExporter traceExporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpActivityExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public OtlpActivityExporter(ExporterOptions options)
        {
            this.traceExporter = new TraceExporter(options);
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(
            IEnumerable<Activity> activityBatch,
            CancellationToken cancellationToken)
        {
            return this.traceExporter.ExportAsync(activityBatch.ToOtlpResourceSpans(), cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return this.traceExporter.ShutdownAsync(cancellationToken);
        }
    }
}
