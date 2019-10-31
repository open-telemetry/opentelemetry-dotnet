// <copyright file="LoggingExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace LoggingTracer
{
    /// <summary>
    /// Logger Exporter.
    /// </summary>
    public class LoggingExporter : SpanExporter
    {
        public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LoggingExporter.ExportAsync:");
            foreach (var span in batch)
            {
                AppendIndentedLine(sb, 4, $"Span('{span.Name}', {span.Kind}");
                AppendIndentedLine(sb, 5, $"SpanId: {span.Context.SpanId}");
                AppendIndentedLine(sb, 5, $"ParentSpanId: {span.ParentSpanId}");
                AppendIndentedLine(sb, 5, $"Tracer: {StringifyResource(span.LibraryResource)}");
                foreach (var a in span.Attributes)
                {
                    AppendIndentedLine(sb, 5, $"{a.Key}' : {a.Value}");
                }
            }
            
            Logger.Log(sb.ToString());

            return Task.FromResult(ExportResult.Success);
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            Logger.Log("LoggingExporter.ShutdownAsync()");
            return Task.CompletedTask;
        }

        private static string StringifyResource(Resource resource)
        {
            return string.Join(", ", resource.Labels.Select(l => l.Value));
        }

        private static void AppendIndentedLine(StringBuilder sb, int indentationLevel, string line)
        {
            for (int i = 0; i < indentationLevel; i++)
            {
                sb.Append('\t');
            }

            sb.AppendLine(line);
        }
    }
}
