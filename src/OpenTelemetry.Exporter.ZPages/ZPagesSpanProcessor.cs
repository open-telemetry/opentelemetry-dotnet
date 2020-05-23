// <copyright file="ZPagesSpanProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Trace.Export;
using Timer = System.Timers.Timer;

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// Implements the zpages span processor that exports spans in OnEnd call without batching.
    /// </summary>
    public class ZPagesSpanProcessor : SpanProcessor
    {
        private readonly ZPagesExporter exporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesSpanProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Zpage Span processor instance.</param>
        public ZPagesSpanProcessor(ZPagesExporter exporter)
        {
            this.exporter = exporter;
        }

        /// <inheritdoc />
        public override void OnStart(SpanData span)
        {
            if (!ZPagesSpans.ProcessingSpanList.ContainsKey(span.Name))
            {
                // If the span name is not in the processing span list, add it to the span list, the total count list, the ended count list and the error count list.
                ZPagesSpans.ProcessingSpanList.Add(span.Name, 1);
                ZPagesSpans.TotalSpanCount.Add(span.Name, 1);
                ZPagesSpans.TotalEndedSpanCount.Add(span.Name, 0);
                ZPagesSpans.TotalSpanErrorCount.Add(span.Name, 0);
                ZPagesSpans.TotalSpanLatency.Add(span.Name, 0);
            }
            else
            {
                // If the span name already exists, then increment the numbers in processing list as well as the total count list.
                ZPagesSpans.ProcessingSpanList.TryGetValue(span.Name, out var activeCount);
                ZPagesSpans.ProcessingSpanList[span.Name] = activeCount + 1;
                ZPagesSpans.TotalSpanCount.TryGetValue(span.Name, out var totalCount);
                ZPagesSpans.TotalSpanCount[span.Name] = totalCount + 1;
            }
        }

        /// <inheritdoc />
        public override void OnEnd(SpanData span)
        {
            try
            {
                // do not await, just start export
                // it can still throw in synchronous part
                _ = this.exporter.ExportAsync(new[] { span }, CancellationToken.None);

                // If the span name is not in the current minute list, add it to the span list.
                if (!ZPagesSpans.CurrentMinuteSpanList.ContainsKey(span.Name))
                {
                    ZPagesSpans.CurrentMinuteSpanList.TryAdd(span.Name, new ZPagesSpanInformation(span));
                }

                // If the span name is not in the current hour list, add it to the span list.
                if (!ZPagesSpans.CurrentHourSpanList.ContainsKey(span.Name))
                {
                    ZPagesSpans.CurrentHourSpanList.TryAdd(span.Name, new ZPagesSpanInformation(span));
                }

                ZPagesSpans.CurrentMinuteSpanList.TryGetValue(span.Name, out var minuteSpanInformation);
                ZPagesSpans.CurrentHourSpanList.TryGetValue(span.Name, out var hourSpanInformation);
                ZPagesSpans.ProcessingSpanList.TryGetValue(span.Name, out var activeCount);

                // Decrement the active span count in processing list, Increment the count of ended spans and calculate the average latency values for one minute and one hour.
                ZPagesSpans.ProcessingSpanList[span.Name] = activeCount - 1;
                minuteSpanInformation.EndedCount++;
                hourSpanInformation.EndedCount++;
                minuteSpanInformation.TotalLatency += span.EndTimestamp.ToUnixTimeMilliseconds() - span.StartTimestamp.ToUnixTimeMilliseconds();
                hourSpanInformation.TotalLatency += span.EndTimestamp.ToUnixTimeMilliseconds() - span.StartTimestamp.ToUnixTimeMilliseconds();
                ZPagesSpans.TotalSpanLatency[span.Name] += span.EndTimestamp.ToUnixTimeMilliseconds() - span.StartTimestamp.ToUnixTimeMilliseconds();
                minuteSpanInformation.GetTotalAverageLatency();
                hourSpanInformation.GetTotalAverageLatency();
                ZPagesSpans.TotalEndedSpanCount.TryGetValue(span.Name, out var endedCount);
                ZPagesSpans.TotalEndedSpanCount[span.Name] = endedCount + 1;

                // Increment the error count, if it applies in all applicable lists.
                if (!span.Status.IsOk)
                {
                    minuteSpanInformation.ErrorCount++;
                    hourSpanInformation.ErrorCount++;
                    ZPagesSpans.TotalSpanErrorCount.TryGetValue(span.Name, out var errorCount);
                    ZPagesSpans.TotalSpanErrorCount[span.Name] = errorCount + 1;
                }

                // Set the last updated timestamp.
                minuteSpanInformation.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                hourSpanInformation.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            }
            catch (Exception exception)
            {
                // Log.SpanProcessorException("OnEnd", ex);
                Console.Write("OnEnd", exception);
            }
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
