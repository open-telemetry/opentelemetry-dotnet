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
    internal struct DataPoint<T> : IDataPoint
        where T : struct
    {
        internal T Value;

        private static int lastTick = -1;
        private static DateTimeOffset lastTimestamp = DateTimeOffset.MinValue;

        private DateTimeOffset timestamp;

        private KeyValuePair<string, object>[] tags;

        public DataPoint(T value, KeyValuePair<string, object>[] tags)
        {
            this.timestamp = DataPoint<T>.GetDateTimeOffset();
            this.Value = value;
            this.tags = tags;
        }

        private DataPoint(DateTimeOffset timestamp, T value, KeyValuePair<string, object>[] tags)
        {
            this.timestamp = timestamp;
            this.Value = value;
            this.tags = tags;
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

        public string ValueAsString()
        {
            return this.Value.ToString();
        }

        public IDataPoint Clone(KeyValuePair<string, object>[] tags)
        {
            return new DataPoint<T>(this.timestamp, this.Value, tags);
        }

        public IDataPoint Clone()
        {
            return new DataPoint<T>(this.timestamp, this.Value, this.tags);
        }

        /// <summary>
        /// Reset (e.g. new DataPointT(...)) this object for reused.
        /// This is provided to avoid additional allocation in Hot Path.
        /// </summary>
        /// <typeparam name="T1">T.</typeparam>
        /// <param name="value">value.</param>
        /// <param name="tags">tags.</param>
        internal void Reset<T1>(T1 value, KeyValuePair<string, object>[] tags)
            where T1 : struct
        {
            this.timestamp = DataPoint<T>.GetDateTimeOffset();
            this.tags = tags;
            this.Value = (T)(object)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DateTimeOffset GetDateTimeOffset()
        {
            int tick = Environment.TickCount;
            if (tick == DataPoint<T>.lastTick)
            {
                return DataPoint<T>.lastTimestamp;
            }

            var dt = DateTimeOffset.UtcNow;
            DataPoint<T>.lastTimestamp = dt;
            DataPoint<T>.lastTick = tick;

            return dt;
        }
    }
}
