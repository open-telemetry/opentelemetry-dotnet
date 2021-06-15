// <copyright file="MetricConsoleExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Metrics
{
    public class MetricConsoleExporter : MetricProcessor
    {
        private string name;

        public MetricConsoleExporter(string name)
        {
            this.name = name;
        }

        public override void OnEnd(MetricItem data)
        {
            foreach (var metric in data.Metrics)
            {
                var tags = metric.Attributes.ToArray().Select(k => $"{k.Key}={k.Value?.ToString()}");

                var kind = metric.GetType().Name;
                var value = metric.ToDisplayString();

                string time = $"{metric.StartTimeExclusive.ToLocalTime().ToString("HH:mm:ss.fff")} {metric.EndTimeInclusive.ToLocalTime().ToString("HH:mm:ss.fff")}";

                var msg = $"Export[{this.name}] {time} {metric.Name} [{string.Join(";", tags)}] {kind} {value}";
                Console.WriteLine(msg);
            }
        }
    }
}
