// <copyright file="CounterSDK.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    public class CounterSDK<T> : Counter<T>
        where T : struct
    {        
        private readonly IDictionary<LabelSet, CounterHandleSDK<T>> counterHandles = new ConcurrentDictionary<LabelSet, CounterHandleSDK<T>>();
        private string metricName;

        public CounterSDK()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public CounterSDK(string name) : this()
        {
            this.metricName = name;
        }

        public override CounterHandle<T> GetHandle(LabelSet labelset)
        {
            if (!this.counterHandles.TryGetValue(labelset, out var handle))
            {
                handle = new CounterHandleSDK<T>();

                this.counterHandles.Add(labelset, handle);
            }

            return handle;
        }

        public override CounterHandle<T> GetHandle(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return this.GetHandle(new LabelSetSDK(labels));
        }

        internal IDictionary<LabelSet, CounterHandleSDK<T>> GetAllHandles()
        {
            return this.counterHandles;
        }
    }
}
