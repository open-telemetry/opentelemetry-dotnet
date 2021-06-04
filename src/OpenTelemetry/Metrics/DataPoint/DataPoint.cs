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

namespace OpenTelemetry.Metrics
{
    internal readonly struct DataPoint : IDataPoint
    {
        private static readonly KeyValuePair<string, object>[] EmptyTag = new KeyValuePair<string, object>[0];

        private readonly Type valueType;
        private readonly int intValue;
        private readonly double doubleValue;

        internal DataPoint(DateTimeOffset timestamp, int value, KeyValuePair<string, object>[] tags)
        {
            this.Timestamp = timestamp;
            this.Tags = tags;
            this.valueType = value.GetType();
            this.intValue = value;
            this.doubleValue = 0;
        }

        internal DataPoint(DateTimeOffset timestamp, double value, KeyValuePair<string, object>[] tags)
        {
            this.Timestamp = timestamp;
            this.Tags = tags;
            this.valueType = value.GetType();
            this.intValue = 0;
            this.doubleValue = value;
        }

        internal DataPoint(DateTimeOffset timestamp, int value)
            : this(timestamp, value, DataPoint.EmptyTag)
        {
        }

        internal DataPoint(DateTimeOffset timestamp, double value)
            : this(timestamp, value, DataPoint.EmptyTag)
        {
        }

        public DateTimeOffset Timestamp { get; }

        public readonly KeyValuePair<string, object>[] Tags { get; }

        public object Value
        {
            get
            {
                if (this.valueType == typeof(int))
                {
                    return this.intValue;
                }
                else if (this.valueType == typeof(double))
                {
                    return this.doubleValue;
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
