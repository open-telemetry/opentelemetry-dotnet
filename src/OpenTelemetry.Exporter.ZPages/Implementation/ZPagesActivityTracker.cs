// <copyright file="ZPagesActivityTracker.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Timers;

namespace OpenTelemetry.Exporter.ZPages.Implementation
{
    /// <summary>
    /// Implements the ZPages Activity Queue which stores all the required activity information.
    /// </summary>
    internal static class ZPagesActivityTracker
    {
        private static readonly long StartTime;

        /// <summary>
        /// Initializes static members of the <see cref="ZPagesActivityTracker"/> class.
        /// </summary>
        static ZPagesActivityTracker()
        {
            ZQueue = new LinkedList<Dictionary<string, ZPagesActivityAggregate>>();
            ProcessingList = new Dictionary<string, long>();
            CurrentMinuteList = new ConcurrentDictionary<string, ZPagesActivityAggregate>();
            CurrentHourList = new ConcurrentDictionary<string, ZPagesActivityAggregate>();
            TotalCount = new Dictionary<string, long>();
            TotalEndedCount = new Dictionary<string, long>();
            TotalErrorCount = new Dictionary<string, long>();
            TotalLatency = new Dictionary<string, long>();
            StartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Gets or sets ZQueue which stores the minute-wise activity information.
        /// </summary>
        public static LinkedList<Dictionary<string, ZPagesActivityAggregate>> ZQueue { get; set; }

        /// <summary>
        /// Gets or sets the current minute activity information list.
        /// </summary>
        public static ConcurrentDictionary<string, ZPagesActivityAggregate> CurrentMinuteList { get; set; }

        /// <summary>
        /// Gets or sets the current hour activity information list.
        /// </summary>
        public static ConcurrentDictionary<string, ZPagesActivityAggregate> CurrentHourList { get; set; }

        /// <summary>
        /// Gets or sets the processing activity information list. This holds the names of the activities which have not ended yet, along with the active count.
        /// </summary>
        public static Dictionary<string, long> ProcessingList { get; set; }

        /// <summary>
        /// Gets or sets the count of activities name-wise.
        /// </summary>
        public static Dictionary<string, long> TotalCount { get; set; }

        /// <summary>
        /// Gets or sets the count of ended activities name-wise.
        /// </summary>
        public static Dictionary<string, long> TotalEndedCount { get; set; }

        /// <summary>
        /// Gets or sets the count of activity errors according to activity name.
        /// </summary>
        public static Dictionary<string, long> TotalErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the total latency of activities name-wise.
        /// </summary>
        public static Dictionary<string, long> TotalLatency { get; set; }

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
            // Enqueue the last minute's activity information list to ZQueue
            ZQueue.AddFirst(new Dictionary<string, ZPagesActivityAggregate>(CurrentMinuteList));

            // Clear the current minute activity list to start recording new activities only
            CurrentMinuteList.Clear();

            // Remove the stale activity information which is at the end of the list
            if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - StartTime >= RetentionTime)
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
            // Clear the last hour's activity information list
            CurrentHourList.Clear();
        }

        internal static void Reset()
        {
            TotalCount.Clear();
            TotalLatency.Clear();
            CurrentHourList.Clear();
            CurrentMinuteList.Clear();
            ZQueue.Clear();
            ProcessingList.Clear();
            TotalEndedCount.Clear();
            TotalErrorCount.Clear();
        }
    }
}
