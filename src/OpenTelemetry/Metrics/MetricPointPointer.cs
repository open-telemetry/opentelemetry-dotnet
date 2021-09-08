// <copyright file="MetricPointPointer.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace OpenTelemetry.Metrics
{
    public readonly struct MetricPointPointer
    {
        private readonly Memory<MetricPoint> point;

        internal MetricPointPointer(
            Memory<MetricPoint> point)
        {
            if (point.Length != 1)
            {
                // TODO: throw
            }

            this.point = point;
        }

        public long LongValue
        {
            get
            {
                return this.point.Span[0].LongValue;
            }
        }

        public double DoubleValue
        {
            get
            {
                return this.point.Span[0].DoubleValue;
            }
        }

        public string[] Keys
        {
            get
            {
                return this.point.Span[0].Keys;
            }
        }

        public object[] Values
        {
            get
            {
                return this.point.Span[0].Values;
            }
        }

        public long[] BucketCounts
        {
            get
            {
                return this.point.Span[0].BucketCounts;
            }
        }

        public double[] ExplicitBounds
        {
            get
            {
                return this.point.Span[0].ExplicitBounds;
            }
        }

        public DateTimeOffset StartTime
        {
            get
            {
                return this.point.Span[0].StartTime;
            }
        }

        public DateTimeOffset EndTime
        {
            get
            {
                return this.point.Span[0].EndTime;
            }
        }
    }
}
