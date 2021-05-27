// <copyright file="DataPoint.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics
{
    internal readonly struct DataPoint : IDataPoint
    {
        internal readonly Type ValueType;
        internal readonly int IntValue;
        internal readonly double DoubleValue;

        private readonly DateTimeOffset timestamp;

        private readonly KeyValuePair<string, object>[] tags;

        internal DataPoint(DateTimeOffset timestamp, int value, KeyValuePair<string, object>[] tags)
        {
            this.timestamp = timestamp;
            this.tags = tags;
            this.ValueType = value.GetType();
            this.IntValue = value;
            this.DoubleValue = 0;
        }

        internal DataPoint(DateTimeOffset timestamp, double value, KeyValuePair<string, object>[] tags)
        {
            this.timestamp = timestamp;
            this.tags = tags;
            this.ValueType = value.GetType();
            this.IntValue = 0;
            this.DoubleValue = value;
        }

        public KeyValuePair<string, object>[] Tags
        {
            get
            {
                return this.tags;
            }
        }

        public DateTimeOffset Timestamp
        {
            get
            {
                return this.timestamp;
            }
        }

        public object Value
        {
            get
            {
                if (this.ValueType == typeof(int))
                {
                    return this.IntValue;
                }
                else if (this.ValueType == typeof(double))
                {
                    return this.DoubleValue;
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }
            }
        }

        internal static DataPoint CreateDataPoint<T>(DateTimeOffset timestamp, T value, KeyValuePair<string, object>[] tags)
        {
            DataPoint dp;

            if (typeof(T) == typeof(int))
            {
                dp = new DataPoint(timestamp, (int)(object)value, tags);
            }
            else if (typeof(T) == typeof(double))
            {
                dp = new DataPoint(timestamp, (double)(object)value, tags);
            }
            else
            {
                throw new Exception("Unsupported Type");
            }

            return dp;
        }
    }
}
