// <copyright file="ZPagesSpans.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;

namespace OpenTelemetry.Exporter.ZPages.Implementation
{
    /// <summary>
    /// Implements the ZPages Span Queue which stores all the required span information.
    /// </summary>
    public static class ZPagesSpans
    {
        private static long startTime;

        /// <summary>
        /// Initializes static members of the <see cref="ZPagesSpans"/> class.
        /// </summary>
        static ZPagesSpans()
        {
            ZQueue = new LinkedList<Dictionary<string, ZPagesSpanInformation>>();
            ProcessingSpanList = new Dictionary<string, long>();
            CurrentMinuteSpanList = new ConcurrentDictionary<string, ZPagesSpanInformation>();
            CurrentHourSpanList = new ConcurrentDictionary<string, ZPagesSpanInformation>();
            TotalSpanCount = new Dictionary<string, long>();
            TotalEndedSpanCount = new Dictionary<string, long>();
            TotalSpanErrorCount = new Dictionary<string, long>();
            TotalSpanLatency = new Dictionary<string, long>();
            startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Gets or sets ZQueue which stores the minute-wise span information.
        /// </summary>
        public static LinkedList<Dictionary<string, ZPagesSpanInformation>> ZQueue { get; set; }

        /// <summary>
        /// Gets or sets the current minute span information list.
        /// </summary>
        public static ConcurrentDictionary<string, ZPagesSpanInformation> CurrentMinuteSpanList { get; set; }

        /// <summary>
        /// Gets or sets the current hour span information list.
        /// </summary>
        public static ConcurrentDictionary<string, ZPagesSpanInformation> CurrentHourSpanList { get; set; }

        /// <summary>
        /// Gets or sets the processing span information list. This holds the names of the spans which have not ended yet, along with the active count.
        /// </summary>
        public static Dictionary<string, long> ProcessingSpanList { get; set; }

        /// <summary>
        /// Gets or sets the count of spans name-wise.
        /// </summary>
        public static Dictionary<string, long> TotalSpanCount { get; set; }

        /// <summary>
        /// Gets or sets the count of ended spans name-wise.
        /// </summary>
        public static Dictionary<string, long> TotalEndedSpanCount { get; set; }

        /// <summary>
        /// Gets or sets the count of span errors according to span name.
        /// </summary>
        public static Dictionary<string, long> TotalSpanErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the total latency of spans name-wise.
        /// </summary>
        public static Dictionary<string, long> TotalSpanLatency { get; set; }

        /// <summary>
        /// Gets or sets the retention time (in milliseconds) for the metrics.
        /// </summary>
        public static long RetentionTime { get; set; }

        /// <summary>
        /// Triggers Calculations every minute.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">Event Arguments.</param>
        public static void PurgeCurrentMinuteData(object source, ElapsedEventArgs e)
        {
            // Enqueue the last minute's span information list to ZQueue
            ZQueue.AddFirst(new Dictionary<string, ZPagesSpanInformation>(CurrentMinuteSpanList));

            // Clear the current minute span list to start recording new spans only
            CurrentMinuteSpanList.Clear();

            // Remove the stale span information which is at the end of the list
            if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime >= RetentionTime)
            {
                ZQueue.RemoveLast();
            }
        }

        /// <summary>
        /// Triggers Calculations every hour.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">Event Arguments.</param>
        public static void PurgeCurrentHourData(object source, ElapsedEventArgs e)
        {
            // Clear the last hour's span information list
            CurrentHourSpanList.Clear();
        }
    }
}
