// <copyright file="ExportComponent.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Export
{
    using OpenCensus.Common;
    using OpenCensus.Internal;

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

        public override ISpanExporter SpanExporter { get; }

        public override IRunningSpanStore RunningSpanStore { get; }

        public override ISampledSpanStore SampledSpanStore { get; }

        public static IExportComponent CreateWithoutInProcessStores(IEventQueue eventQueue)
        {
            return new ExportComponent(false, eventQueue);
        }

        public static IExportComponent CreateWithInProcessStores(IEventQueue eventQueue)
        {
            return new ExportComponent(true, eventQueue);
        }
    }
}
