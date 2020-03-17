// <copyright file="ZPagesSpanInformation.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ZPages.Implementation
{
    /// <summary>
    /// Stores the span information aggregated according to span name.
    /// </summary>
    public class ZPagesSpanInformation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesSpanInformation"/> class.
        /// </summary>
        public ZPagesSpanInformation()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesSpanInformation"/> class when span data is provided.
        /// </summary>
        /// <param name="spanData">Input span data to be used for initialization.</param>
        public ZPagesSpanInformation(SpanData spanData)
        {
            this.Name = spanData.Name;
            this.CountHour = 1;
            this.CountMinute = 1;
            this.CountTotal = 1;
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            this.SpanDataList = new List<SpanData>();
        }

        /// <summary>
        /// Gets or sets the name of the span.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the count in the last minute of the span.
        /// </summary>
        public long CountMinute { get; set; }

        /// <summary>
        /// Gets or sets the count in the last hour of the span.
        /// </summary>
        public long CountHour { get; set; }

        /// <summary>
        /// Gets or sets the total count of the span.
        /// </summary>
        public long CountTotal { get; set; }

        /// <summary>
        /// Gets or sets the total count of the ended span.
        /// </summary>
        public long EndedCount { get; set; }

        /// <summary>
        /// Gets or sets the error count in the last minute of the span.
        /// </summary>
        public long ErrorMinute { get; set; }

        /// <summary>
        /// Gets or sets the error count in the last hour of the span.
        /// </summary>
        public long ErrorHour { get; set; }

        /// <summary>
        /// Gets or sets the total error count of the span.
        /// </summary>
        public long ErrorTotal { get; set; }

        /// <summary>
        /// Gets or sets the average latency in the last minute of the span.
        /// </summary>
        public long AvgLatencyMinute { get; set; }

        /// <summary>
        /// Gets or sets the average latency in the last hour of the span.
        /// </summary>
        public long AvgLatencyHour { get; set; }

        /// <summary>
        /// Gets or sets the total average latency of the span.
        /// </summary>
        public long AvgLatencyTotal { get; set; }

        /// <summary>
        /// Gets or sets the total latency of all spans.
        /// </summary>
        public long TotalLatency { get; set; }

        /// <summary>
        /// Gets or sets the last updated timestamp.
        /// </summary>
        public long LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the span data associated with the current span.
        /// </summary>
        public List<SpanData> SpanDataList { get; set; }

        /// <summary>
        /// Calculates and returns the span count in the last minute.
        /// </summary>
        /// <returns>Span count in the last minute.</returns>
        public long GetCountInLastMinute()
        {
            this.CountMinute = this.GetStartedCountInLastMilliseconds(60000);
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.CountMinute;
        }

        /// <summary>
        /// Calculates and returns the count in the last hour.
        /// </summary>
        /// <returns>Span count in the last hour.</returns>
        public long GetCountInLastHour()
        {
            this.CountHour = this.GetStartedCountInLastMilliseconds(3600000);
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.CountHour;
        }

        /// <summary>
        /// Calculates and returns the error count in the last minute.
        /// </summary>
        /// <returns>Span error count in the last minute.</returns>
        public long GetErrorCountInLastMinute()
        {
            this.ErrorMinute = this.GetErrorCountInLastMilliseconds(60000);
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.ErrorMinute;
        }

        /// <summary>
        /// Calculates and returns the error count in the last hour.
        /// </summary>
        /// <returns>Span error count in the last hour.</returns>
        public long GetErrorCountInLastHour()
        {
            this.ErrorHour = this.GetErrorCountInLastMilliseconds(3600000);
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.ErrorHour;
        }

        /// <summary>
        /// Calculates and returns the total average latency.
        /// </summary>
        /// <returns>Total average latency.</returns>
        public long GetTotalAverageLatency()
        {
            this.AvgLatencyTotal = this.TotalLatency / this.EndedCount;
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.AvgLatencyTotal;
        }

        /// <summary>
        /// Calculates and returns the average latency in the last minute.
        /// </summary>
        /// <returns>Average latency in the last minute.</returns>
        public long GetAverageLatencyInLastMinute()
        {
            this.AvgLatencyMinute = this.GetAverageLatencyInLastMilliseconds(60000);
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.AvgLatencyMinute;
        }

        /// <summary>
        /// Calculates and returns the average latency in the last hour.
        /// </summary>
        /// <returns>Average latency in the last hour.</returns>
        public long GetAverageLatencyInLastHour()
        {
            this.AvgLatencyHour = this.GetAverageLatencyInLastMilliseconds(3600000);
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.AvgLatencyHour;
        }

        private long GetAverageLatencyInLastMilliseconds(int milliseconds)
        {
            long currentTimestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            long spanCount = this.GetEndedCountInLastMilliseconds(milliseconds);
            long totalLatency = 0;

            foreach (var spanData in this.SpanDataList)
            {
                if (currentTimestamp - spanData.EndTimestamp.ToUnixTimeMilliseconds() < milliseconds)
                {
                    totalLatency += spanData.EndTimestamp.ToUnixTimeMilliseconds() -
                                    spanData.StartTimestamp.ToUnixTimeMilliseconds();
                }
                else if (milliseconds == 3600000)
                {
                    // Delete the span data which is older than one hour
                    this.SpanDataList.Remove(spanData);
                }
            }

            return totalLatency / spanCount;
        }

        private long GetStartedCountInLastMilliseconds(int milliseconds)
        {
            long currentTimestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();

            return this.SpanDataList.Count(spanData => currentTimestamp - spanData.EndTimestamp.ToUnixTimeMilliseconds() < milliseconds);
        }

        private long GetEndedCountInLastMilliseconds(int milliseconds)
        {
            long currentTimestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();

            return this.SpanDataList.Count(spanData => currentTimestamp - spanData.EndTimestamp.ToUnixTimeMilliseconds() < milliseconds);
        }

        private long GetErrorCountInLastMilliseconds(int milliseconds)
        {
            long currentTimestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            long errorCount = 0;

            foreach (var spanData in this.SpanDataList)
            {
                if (currentTimestamp - spanData.EndTimestamp.ToUnixTimeMilliseconds() < milliseconds && !spanData.Status.IsOk)
                {
                    Interlocked.Increment(ref errorCount);
                }
            }

            return errorCount;
        }
    }
}
