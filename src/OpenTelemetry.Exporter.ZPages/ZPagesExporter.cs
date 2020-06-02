﻿// <copyright file="ZPagesExporter.cs" company="OpenTelemetry Authors">
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
        private Timer minuteTimer;
        private Timer hourTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public ZPagesExporter(ZPagesExporterOptions options)
        {
            this.Options = options;

            // Create a timer with one minute interval
            this.minuteTimer = new Timer(60000);
            this.minuteTimer.Elapsed += new ElapsedEventHandler(ZPagesSpans.PurgeCurrentMinuteData);
            this.minuteTimer.Enabled = true;

            // Create a timer with one hour interval
            this.hourTimer = new Timer(3600000);
            this.hourTimer.Elapsed += new ElapsedEventHandler(ZPagesSpans.PurgeCurrentHourData);
            this.hourTimer.Enabled = true;
        }

        /// <inheritdoc />
        public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
        {
            var spanDatas = batch as SpanData[] ?? batch.ToArray();
            return Task.FromResult(ExportResult.Success);
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ExportResult.Success);
        }
    }
}
