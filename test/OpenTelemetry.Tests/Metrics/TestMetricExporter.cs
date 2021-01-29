// <copyright file="TestMetricExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Tests
{
    internal class TestMetricExporter : MetricExporter
    {
        public ConcurrentQueue<Metric> Metrics = new ConcurrentQueue<Metric>();
        private readonly Action onExport;

        public TestMetricExporter(Action onExport)
        {
            this.onExport = onExport;
        }

        public override Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            this.onExport?.Invoke();
            foreach (var metric in metrics)
            {
                this.Metrics.Enqueue(metric);
            }

            return Task.FromResult(ExportResult.Success);
        }
    }
}
