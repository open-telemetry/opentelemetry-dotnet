// <copyright file="ZPagesProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Exporter.ZPages.Implementation;
#if NET452
using OpenTelemetry.Internal;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// Implements the zpages span processor that exports spans in OnEnd call without batching.
    /// </summary>
    public class ZPagesProcessor : BaseProcessor<Activity>
    {
        private readonly ZPagesExporter exporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Zpage Span processor instance.</param>
        public ZPagesProcessor(ZPagesExporter exporter)
        {
            this.exporter = exporter;
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public override void OnStart(Activity activity)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            if (!ZPagesActivityTracker.ProcessingList.ContainsKey(activity.DisplayName))
            {
                // If the span name is not in the processing span list, add it to the span list, the total count list, the ended count list and the error count list.
                ZPagesActivityTracker.ProcessingList.Add(activity.DisplayName, 1);
                ZPagesActivityTracker.TotalCount.Add(activity.DisplayName, 1);
                ZPagesActivityTracker.TotalEndedCount.Add(activity.DisplayName, 0);
                ZPagesActivityTracker.TotalErrorCount.Add(activity.DisplayName, 0);
                ZPagesActivityTracker.TotalLatency.Add(activity.DisplayName, 0);
            }
            else
            {
                // If the span name already exists, then increment the numbers in processing list as well as the total count list.
                ZPagesActivityTracker.ProcessingList.TryGetValue(activity.DisplayName, out var activeCount);
                ZPagesActivityTracker.ProcessingList[activity.DisplayName] = activeCount + 1;
                ZPagesActivityTracker.TotalCount.TryGetValue(activity.DisplayName, out var totalCount);
                ZPagesActivityTracker.TotalCount[activity.DisplayName] = totalCount + 1;
            }
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public override void OnEnd(Activity activity)
        {
            Debug.Assert(activity != null, "Activity should not be null");
            try
            {
                // If the span name is not in the current minute list, add it to the span list.
                if (!ZPagesActivityTracker.CurrentMinuteList.ContainsKey(activity.DisplayName))
                {
                    ZPagesActivityTracker.CurrentMinuteList.TryAdd(activity.DisplayName, new ZPagesActivityAggregate(activity));
                }

                // If the span name is not in the current hour list, add it to the span list.
                if (!ZPagesActivityTracker.CurrentHourList.ContainsKey(activity.DisplayName))
                {
                    ZPagesActivityTracker.CurrentHourList.TryAdd(activity.DisplayName, new ZPagesActivityAggregate(activity));
                }

                ZPagesActivityTracker.CurrentMinuteList.TryGetValue(activity.DisplayName, out var minuteSpanInformation);
                ZPagesActivityTracker.CurrentHourList.TryGetValue(activity.DisplayName, out var hourSpanInformation);
                ZPagesActivityTracker.ProcessingList.TryGetValue(activity.DisplayName, out var activeCount);

                // Decrement the active span count in processing list, Increment the count of ended spans and calculate the average latency values for one minute and one hour.
                ZPagesActivityTracker.ProcessingList[activity.DisplayName] = activeCount - 1;
                minuteSpanInformation.EndedCount++;
                hourSpanInformation.EndedCount++;
                minuteSpanInformation.TotalLatency += (long)activity.Duration.TotalMilliseconds;
                hourSpanInformation.TotalLatency += (long)activity.Duration.TotalMilliseconds;
                ZPagesActivityTracker.TotalLatency[activity.DisplayName] += (long)activity.Duration.TotalMilliseconds;
                minuteSpanInformation.GetTotalAverageLatency();
                hourSpanInformation.GetTotalAverageLatency();
                ZPagesActivityTracker.TotalEndedCount.TryGetValue(activity.DisplayName, out var endedCount);
                ZPagesActivityTracker.TotalEndedCount[activity.DisplayName] = endedCount + 1;

                // Increment the error count, if it applies in all applicable lists.
                if (activity.GetStatus() != Status.Ok)
                {
                    minuteSpanInformation.ErrorCount++;
                    hourSpanInformation.ErrorCount++;
                    ZPagesActivityTracker.TotalErrorCount.TryGetValue(activity.DisplayName, out var errorCount);
                    ZPagesActivityTracker.TotalErrorCount[activity.DisplayName] = errorCount + 1;
                }

                // Set the last updated timestamp.
                minuteSpanInformation.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                hourSpanInformation.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            }
            catch (Exception ex)
            {
                ZPagesExporterEventSource.Log.FailedProcess(ex);
                Console.Write("OnEnd {0}", ex);
            }
        }
    }
}
