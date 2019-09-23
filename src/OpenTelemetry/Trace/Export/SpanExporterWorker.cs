// <copyright file="SpanExporterWorker.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Implementation;

    internal class SpanExporterWorker : IDisposable
    {
        private readonly int bufferSize;
        private readonly BlockingCollection<Span> spans;
        private readonly ConcurrentDictionary<string, IHandler> serviceHandlers = new ConcurrentDictionary<string, IHandler>();
        private readonly TimeSpan scheduleDelay;
        private bool shutdown = false;

        public SpanExporterWorker(int bufferSize, TimeSpan scheduleDelay)
        {
            this.bufferSize = bufferSize;
            this.scheduleDelay = TimeSpan.FromSeconds(scheduleDelay.Seconds);
            this.spans = new BlockingCollection<Span>();
        }

        public void Dispose()
        {
            this.shutdown = true;
            this.spans.CompleteAdding();
        }

        internal void AddSpan(Span span)
        {
            if (!this.spans.IsAddingCompleted)
            {
                if (!this.spans.TryAdd(span))
                {
                    // Log failure, dropped span
                }
            }
        }

        internal async Task ExportAsync(Span export, CancellationToken token)
        {
            var handlers = this.serviceHandlers.Values;
            foreach (var handler in handlers)
            {
                try
                {
                    // TODO: the async handlers could be run in parallel.
                    await handler.ExportAsync(new Span[] { export });
                }
                catch (Exception ex)
                {
                    OpenTelemetryEventSource.Log.ExporterThrownExceptionWarning(ex);
                }
            }
        }

        internal async Task ExportAsync(IEnumerable<Span> export, CancellationToken token)
        {
            var handlers = this.serviceHandlers.Values;
            foreach (var handler in handlers)
            {
                try
                {
                    // TODO: the async handlers could be run in parallel.
                    await handler.ExportAsync(export);
                }
                catch (Exception ex)
                {
                    OpenTelemetryEventSource.Log.ExporterThrownExceptionWarning(ex);
                }
            }
        }

        internal async void Run(object obj)
        {
            var toExport = new List<Span>();
            while (!this.shutdown)
            {
                try
                {
                    if (this.spans.TryTake(out var item, this.scheduleDelay))
                    {
                        // Build up list
                        this.BuildList(item, toExport);

                        // Export them
                        await this.ExportAsync(toExport, CancellationToken.None);

                        // Get ready for next batch
                        toExport.Clear();
                    }

                    if (this.spans.IsCompleted)
                    {
                        break;
                    }
                }
                catch (Exception)
                {
                    // Log
                    return;
                }
            }
        }

        internal void RegisterHandler(string name, IHandler handler)
        {
            this.serviceHandlers[name] = handler;
        }

        internal void UnregisterHandler(string name)
        {
            this.serviceHandlers.TryRemove(name, out var prev);
        }

        private void BuildList(Span item, ICollection<Span> toExport)
        {
            if (item is Span span)
            {
                toExport.Add(span);
            }

            // Grab as many as we can
            while (this.spans.TryTake(out item))
            {
                if (item != null)
                {
                    toExport.Add(item);
                }

                if (toExport.Count >= this.bufferSize)
                {
                    break;
                }
            }
        }
    }
}
