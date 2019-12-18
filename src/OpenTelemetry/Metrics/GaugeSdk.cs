// <copyright file="GaugeSdk.cs" company="OpenTelemetry Authors">
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
    internal class GaugeSDK<T> : Gauge<T>
        where T : struct
    {        
        private readonly IDictionary<LabelSet, GaugeHandleSdk<T>> gaugeHandles = new ConcurrentDictionary<LabelSet, GaugeHandleSdk<T>>();
        private string metricName;

        public GaugeSDK()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public GaugeSDK(string name) : this()
        {
            this.metricName = name;
        }

        public override GaugeHandle<T> GetHandle(LabelSet labelset)
        {
            if (!this.gaugeHandles.TryGetValue(labelset, out var handle))
            {
                handle = new GaugeHandleSdk<T>();

                this.gaugeHandles.Add(labelset, handle);
            }

            return handle;
        }

        public override GaugeHandle<T> GetHandle(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return this.GetHandle(new LabelSetSdk(labels));
        }

        internal IDictionary<LabelSet, GaugeHandleSdk<T>> GetAllHandles()
        {
            return this.gaugeHandles;
        }
    }
}
