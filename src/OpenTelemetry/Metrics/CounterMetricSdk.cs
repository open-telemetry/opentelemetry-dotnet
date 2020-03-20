// <copyright file="CounterMetricSdk.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTelemetry.Context;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    internal class CounterMetricSdk<T> : CounterMetric<T>
        where T : struct
    {
        private readonly IDictionary<LabelSet, BoundCounterMetricSdk<T>> counterBoundInstruments = new ConcurrentDictionary<LabelSet, BoundCounterMetricSdk<T>>();
        private string metricName;
        private object collectLock;

        public CounterMetricSdk()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public CounterMetricSdk(string name, object collectLock) : this()
        {
            this.metricName = name;
            this.collectLock = collectLock;
        }

        public override void Add(in SpanContext context, T value, LabelSet labelset)
        {
            this.Bind(labelset, true).Add(context, value);
        }

        public override void Add(in SpanContext context, T value, IEnumerable<KeyValuePair<string, string>> labels)
        {            
            this.Bind(new LabelSetSdk(labels), true).Add(context, value);
        }

        public override void Add(in DistributedContext context, T value, LabelSet labelset)
        {
            this.Bind(labelset, true).Add(context, value);
        }

        public override void Add(in DistributedContext context, T value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            this.Bind(new LabelSetSdk(labels), true).Add(context, value);
        }

        public override BoundCounterMetric<T> Bind(LabelSet labelset)
        {
            return this.Bind(labelset, false);
        }

        public override BoundCounterMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return this.Bind(new LabelSetSdk(labels), false);
        }

        internal BoundCounterMetric<T> Bind(LabelSet labelset, bool isShortLived)
        {
            if (!this.counterBoundInstruments.TryGetValue(labelset, out var boundInstrument))
            {
                var recStatus = isShortLived ? RecordStatus.UpdatePending : RecordStatus.Bound;
                boundInstrument = new BoundCounterMetricSdk<T>(recStatus);
                this.counterBoundInstruments.Add(labelset, boundInstrument);
            }

            // if boundInstrument is marked for removal, then take the
            // lock and re-add. As Collect() might have removed this.
            if (boundInstrument.Status == RecordStatus.CandidateForRemoval)
            {
                lock (this.collectLock)
                {
                    boundInstrument.Status = RecordStatus.UpdatePending;
                    if (!this.counterBoundInstruments.ContainsKey(labelset))
                    {
                        this.counterBoundInstruments.Add(labelset, boundInstrument);
                    }
                }
            }

            return boundInstrument;
        }        

        internal void UnBind(LabelSet labelSet)
        {
            this.counterBoundInstruments.Remove(labelSet);
        }

        internal IDictionary<LabelSet, BoundCounterMetricSdk<T>> GetAllBoundInstruments()
        {
            return this.counterBoundInstruments;
        }        
    }
}
