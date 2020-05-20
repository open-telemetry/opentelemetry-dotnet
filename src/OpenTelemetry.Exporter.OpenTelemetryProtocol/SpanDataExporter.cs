// <copyright file="SpanDataExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    /// <summary>
    /// The trace exporter using the OpenTelemetry protocol (OTLP).
    /// </summary>
    public class SpanDataExporter : SpanExporter
    {
        private readonly TraceExporter traceExporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanDataExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the exporter.</param>
        public SpanDataExporter(ExporterOptions options)
        {
            this.traceExporter = new TraceExporter(options);
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(
            IEnumerable<SpanData> spanDataList,
            CancellationToken cancellationToken)
        {
            return this.traceExporter.ExportAsync(spanDataList.ToOtlpResourceSpans(), cancellationToken);
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return this.traceExporter.ShutdownAsync(cancellationToken);
        }
    }
}
