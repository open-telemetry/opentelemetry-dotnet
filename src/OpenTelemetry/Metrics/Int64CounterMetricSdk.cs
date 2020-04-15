// <copyright file="Int64CounterMetricSdk.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Context;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    internal class Int64CounterMetricSdk : CounterMetricSdkBase<long>
    {
        public Int64CounterMetricSdk(string name)
            : base(name)
        {
        }

        public override void Add(in SpanContext context, long value, LabelSet labelset)
        {
            // user not using bound instrument. Hence create a  short-lived bound instrument.
            this.Bind(labelset, isShortLived: true).Add(context, value);
        }

        public override void Add(in SpanContext context, long value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            // user not using bound instrument. Hence create a short-lived bound instrument.
            this.Bind(new LabelSetSdk(labels), isShortLived: true).Add(context, value);
        }

        public override void Add(in DistributedContext context, long value, LabelSet labelset)
        {
            // user not using bound instrument. Hence create a  short-lived bound instrument.
            this.Bind(labelset, isShortLived: true).Add(context, value);
        }

        public override void Add(in DistributedContext context, long value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            // user not using bound instrument. Hence create a short-lived bound instrument.
            this.Bind(new LabelSetSdk(labels), isShortLived: true).Add(context, value);
        }

        protected override BoundCounterMetricSdkBase<long> CreateMetric(RecordStatus recordStatus)
        {
            return new Int64BoundCounterMetricSdk(recordStatus);
        }
    }
}
