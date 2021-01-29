// <copyright file="NoopMetricProcessor.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Metrics.Export
{
    internal class NoopMetricProcessor : MetricProcessor
    {
        public override void FinishCollectionCycle(out IEnumerable<Metric> metric)
        {
            metric = Enumerable.Empty<Metric>();
        }

        public override void Process(Metric metric)
        {
        }
    }
}
