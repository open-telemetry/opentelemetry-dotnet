// <copyright file="CounterMetricSdkBase.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    internal abstract class CounterMetricSdkBase<T> : CounterMetric<T>
        where T : struct
    {
        // Lock used to sync with Bind/UnBind.
        private readonly object bindUnbindLock = new object();

        private readonly IDictionary<LabelSet, BoundCounterMetricSdkBase<T>> counterBoundInstruments =
            new ConcurrentDictionary<LabelSet, BoundCounterMetricSdkBase<T>>();

        private string metricName;

        protected CounterMetricSdkBase(string name)
        {
            this.metricName = name;
        }

        public IDictionary<LabelSet, BoundCounterMetricSdkBase<T>> GetAllBoundInstruments()
        {
            return this.counterBoundInstruments;
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
            BoundCounterMetricSdkBase<T> boundInstrument;

            lock (this.bindUnbindLock)
            {
                if (!this.counterBoundInstruments.TryGetValue(labelset, out boundInstrument))
                {
                    var recStatus = isShortLived ? RecordStatus.UpdatePending : RecordStatus.Bound;
                    boundInstrument = this.CreateMetric(recStatus);
                    this.counterBoundInstruments.Add(labelset, boundInstrument);
                }
            }

            switch (boundInstrument.Status)
            {
                case RecordStatus.NoPendingUpdate:
                    boundInstrument.Status = RecordStatus.UpdatePending;
                    break;
                case RecordStatus.CandidateForRemoval:
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

                    break;
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

        protected abstract BoundCounterMetricSdkBase<T> CreateMetric(RecordStatus recordStatus);
    }
}
