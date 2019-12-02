// <copyright file="ConsoleTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Console
{
    /// <summary>
    /// Console trace exporter.
    /// </summary>
    public class ConsoleTraceExporter : SpanExporter
    {
        private readonly ConsoleExporterOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleTraceExporter"/> class.
        /// </summary>
        /// <param name="options">Options to use.</param>
        public ConsoleTraceExporter(ConsoleExporterOptions options)
        {
            this.options = options;
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
        {
            System.Console.WriteLine(JsonConvert.SerializeObject(batch, this.options.PrettyPrint ? Formatting.Indented : Formatting.None));
            return Task.FromResult(ExportResult.Success);
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
