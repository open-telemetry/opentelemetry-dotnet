// <copyright file="ZPagesExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Trace.Export;
using Timer = System.Timers.Timer;

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// Implements ZPages exporter.
    /// </summary>
    public class ZPagesExporter : SpanExporter
    {
        internal readonly ZPagesExporterOptions Options;
        private Dictionary<string, ZPagesSpanInformation> spanList = new Dictionary<string, ZPagesSpanInformation>();
        private IEnumerable<SpanData> batch = new List<SpanData>();
        private Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public ZPagesExporter(ZPagesExporterOptions options)
        {
            this.Options = options;

            // Create a timer with one minute interval
            this.timer = new Timer(60000);
            this.timer.Elapsed += new ElapsedEventHandler(this.TriggerCalculations);
        }

        /// <summary>
        /// Returns the stored list of span information aggregated by span name.
        /// </summary>
        /// <returns>Return span list.</returns>
        public Dictionary<string, ZPagesSpanInformation> GetSpanList()
        {
            return this.spanList;
        }

        /// <inheritdoc />
        public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
        {
            var spanDatas = batch as SpanData[] ?? batch.ToArray();
            this.batch = spanDatas;

            foreach (var spanData in this.batch)
            {
                // If the span name is not in the list, add it to the span list
                if (!this.spanList.ContainsKey(spanData.Name))
                {
                    this.spanList.Add(spanData.Name, new ZPagesSpanInformation(spanData));
                    this.timer.Enabled = true;
                }
                else
                {
                    this.spanList.TryGetValue(spanData.Name, out var spanInformation);
                    if (spanInformation != null)
                    {
                        // Do the following if the span has not ended yet:
                        if (spanData.EndTimestamp.ToUnixTimeMilliseconds() <
                            spanData.StartTimestamp.ToUnixTimeMilliseconds())
                        {
                            // Update the count and the last updated timestamp.
                            spanInformation.CountTotal++;
                            spanInformation.CountHour++;
                            spanInformation.CountMinute++;
                            spanInformation.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                        }

                        // Do the following if the span has ended:
                        if (spanData.EndTimestamp.ToUnixTimeMilliseconds() >
                            spanData.StartTimestamp.ToUnixTimeMilliseconds())
                        {
                            // Add the span data to the span data list.
                            spanInformation.SpanDataList.Add(spanData);

                            // Increment the count of ended spans and calculate the average latency values.
                            spanInformation.EndedCount++;
                            spanInformation.TotalLatency += spanData.EndTimestamp.ToUnixTimeMilliseconds() - spanData.StartTimestamp.ToUnixTimeMilliseconds();
                            spanInformation.GetTotalAverageLatency();
                            spanInformation.GetAverageLatencyInLastHour();
                            spanInformation.GetAverageLatencyInLastMinute();

                            // Increment the error count, if it applies and calculate the error count in the last minute and hour.
                            if (!spanData.Status.IsOk)
                            {
                                spanInformation.ErrorTotal++;
                                spanInformation.GetErrorCountInLastHour();
                                spanInformation.GetErrorCountInLastMinute();
                            }

                            // Set the last updated timestamp.
                            spanInformation.LastUpdated = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                        }
                    }
                }
            }

            return Task.FromResult(ExportResult.Success);
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ExportResult.Success);
        }

        private void TriggerCalculations(object source, ElapsedEventArgs e)
        {
            foreach (var spanName in this.spanList.Keys)
            {
                this.spanList.TryGetValue(spanName, out var spanInformation);

                if (spanInformation != null)
                {
                    spanInformation.GetCountInLastHour();
                    spanInformation.GetCountInLastMinute();

                    spanInformation.GetTotalAverageLatency();
                    spanInformation.GetAverageLatencyInLastHour();
                    spanInformation.GetAverageLatencyInLastMinute();

                    spanInformation.GetErrorCountInLastHour();
                    spanInformation.GetErrorCountInLastMinute();
                }
            }
        }
    }
}
