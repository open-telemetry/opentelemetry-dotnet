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
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace.Export
{
    /// <summary>
    /// Implements simple span processor that exports spans in OnEnd call without batching.
    /// </summary>
    public class SimpleSpanProcessor : SpanProcessor
    {
        private bool disposed = false;

        /// <summary>
        /// Constructs simple processor.
        /// </summary>
        /// <param name="exporter">Span processor instance.</param>
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
            try
            {
                // do not await, just start export
                // it can still throw in synchronous part
                _ = this.Exporter.ExportAsync(new[] { span }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(ex);
            }
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            if (!this.disposed)
            {
                this.disposed = true;
                return this.Exporter.ShutdownAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }
    }
}
