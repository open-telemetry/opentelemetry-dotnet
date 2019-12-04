﻿// <copyright file="GaugeAggregator.cs" company="OpenTelemetry Authors">
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
    public class GaugeAggregator<T> where T : struct
    {
        private T value;
        private DateTime timestamp; 

        internal void Update(T value)
        {
            if (typeof(T) == typeof(double))
            {
                this.value = (T)(object)((double)(object)value);
            }
            else
            {
                this.value = (T)(object)((long)(object)value);
            }

            this.timestamp = DateTime.UtcNow;
        }

        internal T LastValue()
        {
            return this.value;
        }
    }
}
