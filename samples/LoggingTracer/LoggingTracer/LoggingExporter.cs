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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
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
                sb.AppendLine($"\t\t\t\tSpan('{span.Name}', {span.Kind}");
                sb.AppendLine($"\t\t\t\t\tTracer: {StringifyResource(span.LibraryResource)}");
                foreach (var a in span.Attributes)
                {
                    sb.AppendLine($"\t\t\t\t\t{a.Key}' : {a.Value}");
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
    }
}
