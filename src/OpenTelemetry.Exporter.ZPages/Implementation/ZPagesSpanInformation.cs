// <copyright file="ZPagesSpanInformation.cs" company="OpenTelemetry Authors">
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
#if NET452
using OpenTelemetry.Internal;
#endif
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
            this.Count = 1;
            this.EndedCount = 0;
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            this.TotalLatency = 0;
            this.AvgLatencyTotal = 0;
            this.ErrorCount = 0;
        }

        /// <summary>
        /// Gets or sets the name of the span.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the total count of the span.
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// Gets or sets the total count of the ended span.
        /// </summary>
        public long EndedCount { get; set; }

        /// <summary>
        /// Gets or sets the total error count of the span.
        /// </summary>
        public long ErrorCount { get; set; }

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
        /// Calculates and returns the total average latency.
        /// </summary>
        /// <returns>Total average latency.</returns>
        public long GetTotalAverageLatency()
        {
            this.AvgLatencyTotal = this.TotalLatency / this.EndedCount;
            this.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            return this.AvgLatencyTotal;
        }
    }
}
