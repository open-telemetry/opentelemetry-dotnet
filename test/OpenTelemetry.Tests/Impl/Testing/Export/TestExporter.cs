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
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Testing.Export
{
    public class TestExporter : SpanExporter
    {
        private readonly ConcurrentQueue<Span> spanDataList = new ConcurrentQueue<Span>();
        private readonly Action<IEnumerable<Span>> onExport;
        public TestExporter(Action<IEnumerable<Span>> onExport)
        {
            this.onExport = onExport;
        }

        public Span[] ExportedSpans => spanDataList.ToArray();

        public bool WasShutDown { get; private set; } = false;

        public override Task<ExportResult> ExportAsync(IEnumerable<Span> data, CancellationToken cancellationToken)
        {
            this.onExport?.Invoke(data);

            foreach (var s in data)
            {
                this.spanDataList.Enqueue(s);
            }

            return Task.FromResult(ExportResult.Success);
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            this.WasShutDown = true;
            return Task.CompletedTask;
        }
    }
}
