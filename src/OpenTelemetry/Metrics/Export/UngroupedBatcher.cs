// <copyright file="UngroupedBatcher.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics.Export
{
    /// <summary>
    /// Batcher which retains all dimensions/labels.
    /// </summary>
    [Obsolete("Metrics API/SDK is not recommended for production. See https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501 for more information on metrics support.")]
    public class UngroupedBatcher : MetricProcessor
    {
        private List<Metric> metrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="UngroupedBatcher"/> class.
        /// </summary>
        public UngroupedBatcher()
        {
            this.metrics = new List<Metric>();
        }

        /// <inheritdoc/>
        public override void FinishCollectionCycle(out IEnumerable<Metric> metrics)
        {
            // The batcher is currently stateless. i.e it forgets state after collection is done.
            // Once the spec is ready for stateless vs stateful, we need to modify batcher
            // to remember or clear state after each cycle.
            metrics = this.metrics;
            this.metrics = new List<Metric>();
            OpenTelemetrySdkEventSource.Log.BatcherCollectionCompleted(this.metrics.Count);
        }

        /// <inheritdoc/>
        public override void Process(Metric metric)
        {
            this.metrics.Add(metric);
        }
    }
}
