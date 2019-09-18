﻿// <copyright file="SpanExporter.cs" company="OpenTelemetry Authors">
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
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SpanExporter : ISpanExporter
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

        public static ISpanExporter Create(int bufferSize = 32, TimeSpan? scheduleDelay = null)
        {
            var worker = new SpanExporterWorker(bufferSize, scheduleDelay ?? TimeSpan.FromSeconds(5));
            return new SpanExporter(worker);
        }

        public void AddSpan(ISpan span)
        {
            this.worker.AddSpan(span);
        }

        /// <inheritdoc/>
        public Task ExportAsync(SpanData export, CancellationToken token)
        {
            return this.worker.ExportAsync(export, token);
        }

        public void RegisterHandler(string name, IHandler handler)
        {
            this.worker.RegisterHandler(name, handler);
        }

        public void UnregisterHandler(string name)
        {
            this.worker.UnregisterHandler(name);
        }

        public void Dispose()
        {
            this.worker.Dispose();
        }
    }
}
