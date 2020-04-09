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
        // Lock used to sync with Bind/UnBind.
        private object bindUnbindLock = new object();

        public CounterMetricSdk()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public CounterMetricSdk(string name) : this()
        {
            this.metricName = name;
        }

        public override void Add(in SpanContext context, T value, LabelSet labelset)
        {
            // user not using bound instrument. Hence create a
            // short-lived bound intrument.
            this.Bind(labelset, isShortLived: true).Add(context, value);
        }

        public override void Add(in SpanContext context, T value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            // user not using bound instrument. Hence create a
            // short-lived bound intrument.
            this.Bind(new LabelSetSdk(labels), isShortLived: true).Add(context, value);
        }

        public override void Add(in DistributedContext context, T value, LabelSet labelset)
        {
            // user not using bound instrument. Hence create a
            // short-lived bound intrument.
            this.Bind(labelset, isShortLived: true).Add(context, value);
        }

        public override void Add(in DistributedContext context, T value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            // user not using bound instrument. Hence create a
            // short-lived bound intrument.
            this.Bind(new LabelSetSdk(labels), isShortLived: true).Add(context, value);
        }

        public override BoundCounterMetric<T> Bind(LabelSet labelset)
        {
            // user making Bind call means record is not shortlived.
            return this.Bind(labelset, isShortLived: false);
        }

        public override BoundCounterMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // user making Bind call means record is not shortlived.
            return this.Bind(new LabelSetSdk(labels), isShortLived: false);
        }

        internal BoundCounterMetric<T> Bind(LabelSet labelset, bool isShortLived)
        {
            if (!this.counterBoundInstruments.TryGetValue(labelset, out var boundInstrument))
            {
                var recStatus = isShortLived ? RecordStatus.UpdatePending : RecordStatus.Bound;
                boundInstrument = new BoundCounterMetricSdk<T>(recStatus);
                this.counterBoundInstruments.Add(labelset, boundInstrument);
            }

            if (boundInstrument.Status == RecordStatus.NoPendingUpdate)
            {
                boundInstrument.Status = RecordStatus.UpdatePending;
            }
            else if (boundInstrument.Status == RecordStatus.CandidateForRemoval)
            {
                // if boundInstrument is marked for removal, then take the
                // lock to sync with Unbind() and re-add. As Collect() might have called Unbind().

                /*
                 * If Unbind gets the lock first, then it'd have removed the record.
                 * But it gets added again by Bind() so no record is lost.
                 * If Bind method gets this lock first, it'd promote record to UpdatePending, so that
                 * Unbind will leave this record untouched.
                                      
                 * Additional notes:
                 * This lock is never taken for bound instruments, and they offer the fastest performance.
                 * This lock is only taken for those labelsets which are marked CandidateForRemoval.
                 * It means the the 1st time a labelset is re-encountered after two Collect() has occured,
                 * this lock must be taken. Subsequent usage of this labelset before the next two Collect()
                 * will already have status promoted, and no lock is taken.
                 * In effect, the lock is only taken for those labelsets
                 * which was used once, then not used for two collect(), and then used within the subsequent
                 * Collect().
                 *
                 * Its important to note that, for a brand new LabelSet being encountered for the 1st time, lock is not
                 * taken. Lock is taken only during the 1st re-appearance of a LabelSet after a Collect period.
                 *  
                */

                lock (this.bindUnbindLock)
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
            lock (this.bindUnbindLock)
            {
                if (this.counterBoundInstruments.TryGetValue(labelSet, out var boundInstrument))
                {
                    // Check status again, inside lock as an instrument update
                    // might have occured which promoted this record.
                    if (boundInstrument.Status == RecordStatus.CandidateForRemoval)
                    {
                        this.counterBoundInstruments.Remove(labelSet);
                    }
                }
            }            
        }

        internal IDictionary<LabelSet, BoundCounterMetricSdk<T>> GetAllBoundInstruments()
        {
            return this.counterBoundInstruments;
        }
    }
}
