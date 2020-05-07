// <copyright file="ConsoleExporter.cs" company="OpenTelemetry Authors">
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Console
{
    public class ConsoleExporter : SpanExporter
    {
        private readonly JsonSerializerOptions serializerOptions;

        public ConsoleExporter(ConsoleExporterOptions options)
        {
            this.serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = options.Pretty,
            };

            this.serializerOptions.Converters.Add(new JsonStringEnumConverter());
            this.serializerOptions.Converters.Add(new ActivitySpanIdConverter());
            this.serializerOptions.Converters.Add(new ActivityTraceIdConverter());
        }

        public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
        {
            foreach (var span in batch)
            {
                System.Console.WriteLine(JsonSerializer.Serialize(span, this.serializerOptions));
            }

            return Task.FromResult(ExportResult.Success);
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
