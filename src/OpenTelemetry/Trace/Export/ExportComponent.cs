﻿// <copyright file="ExportComponent.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;

    /// <inheritdoc/>
    public sealed class ExportComponent : ExportComponentBase
    {
        private const int ExporterBufferSize = 32;

        // Enforces that trace export exports data at least once every 5 seconds.
        private static readonly Duration ExporterScheduleDelay = Duration.Create(5, 0);

        private ExportComponent(bool supportInProcessStores, IEventQueue eventQueue)
        {
            this.SpanExporter = Export.SpanExporter.Create(ExporterBufferSize, ExporterScheduleDelay);
            this.RunningSpanStore =
                supportInProcessStores
                    ? new InProcessRunningSpanStore()
                    : Export.RunningSpanStoreBase.NoopRunningSpanStore;
            this.SampledSpanStore =
                supportInProcessStores
                    ? new InProcessSampledSpanStore(eventQueue)
                    : Export.SampledSpanStoreBase.NoopSampledSpanStore;
        }

        /// <inheritdoc/>
        public override ISpanExporter SpanExporter { get; }

        /// <inheritdoc/>
        public override IRunningSpanStore RunningSpanStore { get; }

        /// <inheritdoc/>
        public override ISampledSpanStore SampledSpanStore { get; }

        /// <summary>
        /// Constructs a new <see cref="IExportComponent"/> with noop span stores.
        /// </summary>
        /// <param name="eventQueue"><see cref="IEventQueue"/> for sampled span store.</param>
        /// <returns>A new <see cref="IExportComponent"/>.</returns>
        public static IExportComponent CreateWithoutInProcessStores(IEventQueue eventQueue)
        {
            return new ExportComponent(false, eventQueue);
        }

        /// <summary>
        /// Constructs a new <see cref="IExportComponent"/> with in process span stores.
        /// </summary>
        /// <param name="eventQueue"><see cref="IEventQueue"/> for sampled span store.</param>
        /// <returns>A new <see cref="IExportComponent"/>.</returns>
        public static IExportComponent CreateWithInProcessStores(IEventQueue eventQueue)
        {
            return new ExportComponent(true, eventQueue);
        }
    }
}
