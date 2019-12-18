// <copyright file="MeasureSdk.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal class MeasureSdk<T> : Measure<T>
        where T : struct
    {        
        private readonly IDictionary<LabelSet, MeasureHandleSdk<T>> measureHandles = new ConcurrentDictionary<LabelSet, MeasureHandleSdk<T>>();
        private string metricName;

        public MeasureSdk()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public MeasureSdk(string name) : this()
        {
            this.metricName = name;
        }

        public override MeasureHandle<T> GetHandle(LabelSet labelset)
        {
            if (!this.measureHandles.TryGetValue(labelset, out var handle))
            {
                handle = new MeasureHandleSdk<T>();

                this.measureHandles.Add(labelset, handle);
            }

            return handle;
        }

        public override MeasureHandle<T> GetHandle(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return this.GetHandle(new LabelSetSdk(labels));
        }

        internal IDictionary<LabelSet, MeasureHandleSdk<T>> GetAllHandles()
        {
            return this.measureHandles;
        }
    }
}
