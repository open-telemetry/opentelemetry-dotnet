// <copyright file="SumAggregator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    public class SumAggregator<T> where T : struct
    {
        private T sum;

        internal void Update(T value)
        {
            if (typeof(T) == typeof(double))
            {
                this.sum = (T)(object)((double)(object)this.sum + (double)(object)value);
            }
            else
            {
                this.sum = (T)(object)((long)(object)this.sum + (long)(object)value);
            }
        }

        internal T Sum()
        {
            return this.sum;
        }
    }
}
