// <copyright file="SpanExporter.cs" company="OpenTelemetry Authors">
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
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Common;

    public sealed class SpanExporter : SpanExporterBase
    {
        private readonly Thread workerThread;

        private readonly SpanExporterWorker worker;

        internal SpanExporter(SpanExporterWorker worker)
        {
            this.worker = worker;
            this.workerThread = new Thread(worker.Run)
            {
                IsBackground = true,
                Name = "SpanExporter",
            };
            this.workerThread.Start();
        }

        internal Thread ServiceExporterThread
        {
            get
            {
                return this.workerThread;
            }
        }

        public override void AddSpan(ISpan span)
        {
            this.worker.AddSpan(span);
        }

        /// <inheritdoc/>
        public override Task ExportAsync(SpanData export, CancellationToken token)
        {
            return this.worker.ExportAsync(export, token);
        }

        public override void RegisterHandler(string name, IHandler handler)
        {
            this.worker.RegisterHandler(name, handler);
        }

        public override void UnregisterHandler(string name)
        {
            this.worker.UnregisterHandler(name);
        }

        public override void Dispose()
        {
            this.worker.Dispose();
        }

        internal static ISpanExporter Create(int bufferSize, Duration scheduleDelay)
        {
            var worker = new SpanExporterWorker(bufferSize, scheduleDelay);
            return new SpanExporter(worker);
        }
    }
}
