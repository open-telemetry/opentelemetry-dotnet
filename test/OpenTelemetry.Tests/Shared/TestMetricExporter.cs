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
using System.Collections.Generic;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests
{
    internal class TestMetricExporter : MetricExporter
    {
        private readonly Action<IEnumerable<Metric>> processBatchAction;
        private AggregationTemporality temporality;

        public TestMetricExporter(Action<IEnumerable<Metric>> processBatchAction, AggregationTemporality temporality = AggregationTemporality.Cumulative)
        {
            this.processBatchAction = processBatchAction ?? throw new ArgumentNullException(nameof(processBatchAction));
            this.temporality = temporality;
        }

        public override AggregationTemporality GetAggregationTemporality()
        {
            return this.temporality;
        }

        public override ExportResult Export(IEnumerable<Metric> batch)
        {
            this.processBatchAction(batch);

            return ExportResult.Success;
        }
    }
}
