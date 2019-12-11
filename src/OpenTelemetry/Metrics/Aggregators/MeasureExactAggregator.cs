// <copyright file="MeasureExactAggregator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Basic aggregator keeping all raw values.
    /// </summary>
    /// <typeparam name="T">Type of measure instrument.</typeparam>
    public class MeasureExactAggregator<T> : Aggregator<T> 
        where T : struct
    {
        private List<T> values = new List<T>();
        private List<T> checkPoint;

        public override void Checkpoint()
        {
            // TODO = don't lose old checkpoint if it was not exported.
            // Merge with it.
            this.checkPoint = this.values;
            this.values = new List<T>();
        }

        public override void Update(T value)
        {
            if (typeof(T) == typeof(double))
            {
                this.values.Add((T)(object)((double)(object)value));
            }
            else
            {
                this.values.Add((T)(object)((long)(object)value));
            }
        }

        internal List<T> ValueFromLastCheckpoint()
        {
            return this.checkPoint;
        }
    }
}
