// <copyright file="SimpleSpanProcessor.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Trace;

    /// <summary>
    /// Implements simple span processor that exports spans in OnEnd call without batching.
    /// </summary>
    public class SimpleSpanProcessor : SpanProcessor
    {
        public SimpleSpanProcessor(SpanExporter exporter) : base(exporter)
        {
        }

        /// <inheritdoc />
        public override void OnStart(Span span)
        {
        }

        /// <inheritdoc />
        public override void OnEnd(Span span)
        {
            // do not await, just start export
            this.Exporter.ExportAsync(new[] { span }, CancellationToken.None);
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return this.Exporter.ShutdownAsync(cancellationToken);
        }
    }
}
