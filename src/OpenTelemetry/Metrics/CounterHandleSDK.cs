// <copyright file="CounterHandleSDK.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry.Context;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    public class CounterHandleSDK<T> : CounterHandle<T>
        where T : struct
    {
        private readonly MetricProcessor<T> metricProcessor;
        private LabelSet labelset;

        public CounterHandleSDK()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public CounterHandleSDK(string metricName, LabelSet labelset, MetricProcessor<T> metricProcessor) : this()
        {
            this.metricProcessor = metricProcessor;
            this.labelset = labelset;
        }

        public override void Add(in SpanContext context, T value)
        {
            this.metricProcessor.AddCounter(this.labelset, value);
        }

        public override void Add(in DistributedContext context, T value)
        {
            this.metricProcessor.AddCounter(this.labelset, value);
        }
    }
}
