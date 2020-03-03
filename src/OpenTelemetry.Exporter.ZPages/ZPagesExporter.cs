// <copyright file="ZPagesExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Implementation;
using OpenTelemetry.Trace.Export;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public ZPagesExporter(ZPagesExporterOptions options)
        {
            this.Options = options;
        }

        /// <inheritdoc />
        public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
        {
            var spanDatas = batch as SpanData[] ?? batch.ToArray();
            this.batch = spanDatas;

            foreach (var spanData in this.batch)
            {
                if (!this.spanList.ContainsKey(spanData.Name))
                {
                    this.spanList.Add(spanData.Name, new ZPagesSpanInformation(spanData));
                    int count = this.spanList.Count;
                }
                else
                {
                    ZPagesSpanInformation spanInformation;
                    this.spanList.TryGetValue(spanData.Name, out spanInformation);
                    if (spanInformation != null)
                    {
                        spanInformation.SpanDataList.Add(spanData);
                        spanInformation.CountHour++;
                        spanInformation.CountMinute++;
                        spanInformation.CountTotal++;
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
    }
}
