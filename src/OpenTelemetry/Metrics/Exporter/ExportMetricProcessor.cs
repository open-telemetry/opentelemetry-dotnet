// <copyright file="ExportMetricProcessor.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading;

#nullable enable

namespace OpenTelemetry.Metrics
{
    public class ExportMetricProcessor : BaseProcessor<ExportMetricContext>
    {
        public override void OnEnd(ExportMetricContext data)
        {
            foreach (var exports in data.Exports)
            {
                foreach (var item in exports)
                {
                    var msg = $"{item.Key.Meter.Name}:{item.Key.Name} = count:{item.Value.Count}, sum:{item.Value.Sum}";
                    Console.WriteLine($"Export: {msg}");
                }
            }
        }
    }
}
