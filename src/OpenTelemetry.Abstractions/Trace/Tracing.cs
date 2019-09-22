// <copyright file="Tracing.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace.Export;

    /// <summary>
    /// Class that manages a global instance of the <see cref="Tracer"/>.
    /// </summary>
    public static class Tracing
    {
        private static readonly Proxy ProxyInstance = new Proxy();

        /// <summary>
        /// Gets the tracer to record spans.
        /// </summary>
        public static ITracer Tracer { get; private set; } = ProxyInstance;

        /// <summary>
        /// Gets the exporter to use to upload spans.
        /// </summary>
        public static ISpanExporter SpanExporter { get; private set; } = ProxyInstance;

        public static void Init(ITracer tracer, ISpanExporter spanExporter)
        {
            ProxyInstance.ActualTracer = tracer ?? NoopTracer.Instance;
            ProxyInstance.ActualSpanExporter = spanExporter ?? NoopSpanExporter.Instance;
        }

        private class Proxy : ITracer, ISpanExporter
        {
            // ITracer
            public ISpan CurrentSpan => this.ActualTracer.CurrentSpan;

            public IBinaryFormat BinaryFormat => this.ActualTracer.BinaryFormat;

            public ITextFormat TextFormat => this.ActualTracer.TextFormat;

            internal ITracer ActualTracer { get; set; } = NoopTracer.Instance;

            internal ISpanExporter ActualSpanExporter { get; set; } = NoopSpanExporter.Instance;

            public IScope WithSpan(ISpan span) => this.ActualTracer.WithSpan(span);

            public ISpanBuilder SpanBuilder(string spanName) => this.ActualTracer.SpanBuilder(spanName);

            public void RecordSpanData(SpanData span) => this.ActualTracer.RecordSpanData(span);

            // ISpanExporter
            public void Dispose() => this.ActualSpanExporter.Dispose();

            public void AddSpan(ISpan span) => this.ActualSpanExporter.AddSpan(span);

            public Task ExportAsync(SpanData export, CancellationToken token)
                => this.ActualSpanExporter.ExportAsync(export, token);

            public void RegisterHandler(string name, IHandler handler) =>
                this.ActualSpanExporter.RegisterHandler(name, handler);

            public void UnregisterHandler(string name) => this.ActualSpanExporter.UnregisterHandler(name);
        }
    }
}
