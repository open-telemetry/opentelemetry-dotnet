// <copyright file="Global.asax.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;

#pragma warning disable CS0618

namespace GroceryExample
{
    public class MyLabelSet : LabelSet
    {
        public MyLabelSet(params KeyValuePair<string,string>[] labels)
        {
            List<KeyValuePair<string,string>> list = new List<KeyValuePair<string, string>>();
            foreach (var kv in labels)
            {
                list.Add(kv);
            }

            Labels = list;
        }

        public override IEnumerable<KeyValuePair<string, string>> Labels { get; set; } = System.Linq.Enumerable.Empty<KeyValuePair<string, string>>();
    }

    public class MyMetricProcessor : MetricProcessor
    {
        private List<Metric> items = new List<Metric>();

        public override void FinishCollectionCycle(out IEnumerable<Metric> metrics)
        {
            metrics = Interlocked.Exchange(ref items, new List<Metric>());
        }

        public override void Process(Metric metric)
        {
            items.Add(metric);
        }
    }

    public class MyMetricExporter: MetricExporter
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