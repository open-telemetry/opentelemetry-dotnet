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
using System.Diagnostics;
using System.Timers;
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Trace;
using Timer = System.Timers.Timer;

namespace OpenTelemetry.Exporter.ZPages
{
    /// <summary>
    /// Implements ZPages exporter.
    /// </summary>
    public class ZPagesExporter : ActivityExporter
    {
        internal readonly ZPagesExporterOptions Options;
        private readonly Timer minuteTimer;
        private readonly Timer hourTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZPagesExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public ZPagesExporter(ZPagesExporterOptions options)
        {
            ZPagesActivityTracker.RetentionTime = options?.RetentionTime ?? throw new ArgumentNullException(nameof(options));

            this.Options = options;

            // Create a timer with one minute interval
            this.minuteTimer = new Timer(60000);
            this.minuteTimer.Elapsed += new ElapsedEventHandler(ZPagesActivityTracker.PurgeCurrentMinuteData);
            this.minuteTimer.Enabled = true;

            // Create a timer with one hour interval
            this.hourTimer = new Timer(3600000);
            this.hourTimer.Elapsed += new ElapsedEventHandler(ZPagesActivityTracker.PurgeCurrentHourData);
            this.hourTimer.Enabled = true;
        }

        /// <inheritdoc />
        public override ExportResult Export(in Batch<Activity> batch)
        {
            // var spanDatas = batch as SpanData[] ?? batch.ToArray();
            return ExportResult.Success;
        }
    }
}
