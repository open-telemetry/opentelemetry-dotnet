// <copyright file="BoundCounterMetricSdk.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    internal class BoundCounterMetricSdk<T> : BoundCounterMetric<T>
        where T : struct
    {
        internal RecordStatus Status;
        private readonly CounterSumAggregator<T> sumAggregator = new CounterSumAggregator<T>();

        internal BoundCounterMetricSdk()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        internal BoundCounterMetricSdk(RecordStatus recordStatus) : this()
        {
            this.Status = recordStatus;
        }

        public override void Add(in SpanContext context, T value)
        {
            this.sumAggregator.Update(value);
        }

        public override void Add(in DistributedContext context, T value)
        {
            this.sumAggregator.Update(value);
        }

        internal CounterSumAggregator<T> GetAggregator()
        {
            return this.sumAggregator;
        }
    }
}
