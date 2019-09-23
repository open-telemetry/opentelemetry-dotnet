﻿// <copyright file="SpanExporterBase.cs" company="OpenTelemetry Authors">
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

    public abstract class SpanExporterBase : ISpanExporter
    {
        public static ISpanExporter NoopSpanExporter { get; } = new NoopSpanExporter();

        public abstract void AddSpan(Span span);

        /// <inheritdoc/>
        public abstract Task ExportAsync(Span export, CancellationToken token);

        public abstract void Dispose();

        public abstract void RegisterHandler(string name, IHandler handler);

        public abstract void UnregisterHandler(string name);
    }
}
