// <copyright file="TestExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Testing.Export
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    public class TestExporter : SpanExporter
    {
        private readonly bool throwOnExport = false;
        public TestExporter(bool throwOnExport)
        {
            this.throwOnExport = throwOnExport;
        }

        private readonly object monitor = new object();
        private readonly List<Span> spanDataList = new List<Span>();

        public override Task<ExportResult> ExportAsync(IEnumerable<Span> data, CancellationToken cancellationToken)
        {
            lock (monitor)
            {
                this.spanDataList.AddRange(data);

                if (this.throwOnExport)
                {
                    throw new ArgumentException("no export for you");
                }

                Monitor.PulseAll(monitor);
            }

            return Task.FromResult(ExportResult.Success);
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public IEnumerable<Span> WaitForExport(int numberOfSpans)
        {
            var result = new List<Span>();
            lock (monitor) {
                while (spanDataList.Count < numberOfSpans)
                {
                    try
                    {
                        if (!Monitor.Wait(monitor, 5000))
                        {
                            return result;
                        }
                    }
                    catch (Exception)
                    {
                        // Preserve the interruption status as per guidance.
                        // Thread.currentThread().interrupt();
                        return result;
                    }
                }
                result = new List<Span>(spanDataList);
                spanDataList.Clear();
            }
            return result;
        }
    }
}
