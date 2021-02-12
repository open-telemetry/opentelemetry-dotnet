// <copyright file="MyMetricExporter.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Export;

#pragma warning disable CS0618

namespace GroceryExample
{
    public class MyMetricExporter : MetricExporter
    {
        public override Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            return Task.Run<ExportResult>(() =>
            {
                StringBuilder sb = new StringBuilder();

                foreach (var m in metrics)
                {
                    sb.AppendLine($"[{m.MetricNamespace}:{m.MetricName}]");

                    foreach (var data in m.Data)
                    {
                        sb.Append("    Labels: ");
                        foreach (var l in data.Labels)
                        {
                            sb.Append($"{l.Key}={l.Value}, ");
                        }

                        sb.AppendLine();
                    }
                }

                Console.WriteLine(sb.ToString());

                return ExportResult.Success;
            });
        }
    }
}
