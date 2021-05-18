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
        public override void OnEnd(MetricItem data)
        {
            foreach (var metric in data.Metrics)
            {
                var tags = metric.Point?.Tags.ToArray().Select(k => $"{k.Key}={k.Value?.ToString()}");
                var msg = $"{metric.Name}[{string.Join(";", tags)}] = {metric.Point?.ValueAsString}";
                Console.WriteLine($"Export: {msg}");
            }
        }
    }
}
