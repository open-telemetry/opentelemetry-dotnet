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

using System.Collections.Generic;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ZPages.Implementation
{
    internal class ZPagesSpanInformation
    {
        public ZPagesSpanInformation()
        {
        }

        public ZPagesSpanInformation(SpanData spanData)
        {
            this.Name = spanData.Name;
            this.CountHour = 1;
            this.CountMinute = 1;
            this.CountTotal = 1;
            this.SpanDataList = new List<SpanData>();
            this.SpanDataList.Add(spanData);
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
        /// Gets or sets the span data associated with the current span.
        /// </summary>
        public List<SpanData> SpanDataList { get; set; }
    }
}
