// <copyright file="MetricPoint.cs" company="OpenTelemetry Authors">
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
    public struct MetricPoint
    {
        private long longVal;
        private long lastLongSum;
        private double doubleVal;
        private double lastDoubleSum;

        private readonly AggregationType AggType { get; }

        internal MetricPoint(AggregationType aggType, DateTimeOffset startTime, string[] keys, object[] values)
        {
            this.AggType = aggType;
            this.StartTime = startTime;
            this.Keys = keys;
            this.Values = values;
            this.EndTime = default;
            this.LongValue = default;
            this.longVal = default;
            this.lastLongSum = default;
            this.DoubleValue = default;
            this.doubleVal = default;
            this.lastDoubleSum = default;
        }

        public readonly string[] Keys;

        public readonly object[] Values;

        public DateTimeOffset StartTime { get; internal set; }

        public DateTimeOffset EndTime { get; internal set; }

        public long LongValue { get; internal set; }

        public double DoubleValue { get; internal set; }

        internal void Update(long number)
        {
            switch (this.AggType)
            {
                case AggregationType.LongSumIncomingDelta:
                    {
                        Interlocked.Add(ref this.longVal, number);
                        break;
                    }

                case AggregationType.LongSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.longVal, number);
                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        Interlocked.Exchange(ref this.longVal, number);
                        break;
                    }
            }
        }

        internal void Update(double number)
        {
            switch (this.AggType)
            {
                case AggregationType.DoubleSumIncomingDelta:
                    {
                        double initValue, newValue;
                        do
                        {
                            initValue = this.doubleVal;
                            newValue = initValue + number;
                        }
                        while (initValue != Interlocked.CompareExchange(ref this.doubleVal, newValue, initValue));
                        break;
                    }

                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        Interlocked.Exchange(ref this.doubleVal, number);
                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        Interlocked.Exchange(ref this.doubleVal, number);
                        break;
                    }
            }
        }

        internal void TakeSnapShot(bool outputDelta)
        {
            switch (this.AggType)
            {
                case AggregationType.LongSumIncomingDelta:
                case AggregationType.LongSumIncomingCumulative:
                    {
                        if (outputDelta)
                        {
                            long initValue = Interlocked.Read(ref this.longVal);
                            this.LongValue = initValue - this.lastLongSum;
                            this.lastLongSum = initValue;
                        }
                        else
                        {
                            this.LongValue = Interlocked.Read(ref this.longVal);
                        }

                        break;
                    }

                case AggregationType.DoubleSumIncomingDelta:
                case AggregationType.DoubleSumIncomingCumulative:
                    {
                        if (outputDelta)
                        {
                            // TODO:
                            // Is this thread-safe way to read double?
                            // As long as the value is not -ve infinity,
                            // the exchange (to 0.0) will never occur,
                            // but we get the original value atomically.
                            double initValue = Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity);
                            this.DoubleValue = initValue - this.lastDoubleSum;
                            this.lastDoubleSum = initValue;
                        }
                        else
                        {
                            // TODO:
                            // Is this thread-safe way to read double?
                            // As long as the value is not -ve infinity,
                            // the exchange (to 0.0) will never occur,
                            // but we get the original value atomically.
                            this.DoubleValue = Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity);
                        }

                        break;
                    }

                case AggregationType.LongGauge:
                    {
                        this.LongValue = Interlocked.Read(ref this.longVal);
                        break;
                    }

                case AggregationType.DoubleGauge:
                    {
                        // TODO:
                        // Is this thread-safe way to read double?
                        // As long as the value is not -ve infinity,
                        // the exchange (to 0.0) will never occur,
                        // but we get the original value atomically.
                        this.DoubleValue = Interlocked.CompareExchange(ref this.doubleVal, 0.0, double.NegativeInfinity);
                        break;
                    }
            }
        }
    }
}
