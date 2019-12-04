// <copyright file="MeasureHandleSDK.cs" company="OpenTelemetry Authors">
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
    public class MeasureHandleSDK<T> : MeasureHandle<T>
        where T : struct
    {
        private readonly MeasureExactAggregator<T> measureExactAggregator = new MeasureExactAggregator<T>();

        internal MeasureHandleSDK()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public override void Record(in SpanContext context, T value)
        {
            this.measureExactAggregator.Update(value);
        }

        public override void Record(in DistributedContext context, T value)
        {
            this.measureExactAggregator.Update(value);
        }

        internal MeasureExactAggregator<T> GetAggregator()
        {
            return this.measureExactAggregator;
        }
    }
}
