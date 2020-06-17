// <copyright file="SimpleSpanProcessor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
    public class SimpleSpanProcessor : SpanProcessor, IDisposable
    {
        private readonly SpanExporter exporter;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleSpanProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Span processor instance.</param>
        public SimpleSpanProcessor(SpanExporter exporter)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <inheritdoc />
        public override void OnStart(SpanData span)
        {
        }

        /// <inheritdoc />
        public override void OnEnd(SpanData span)
        {
            try
            {
                // do not await, just start export
                // it can still throw in synchronous part
                _ = this.exporter.ExportAsync(new[] { span }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException("OnEnd", ex);
            }
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            if (!this.disposed)
            {
                this.disposed = true;
                return this.exporter.ShutdownAsync(cancellationToken);
            }

#if NET452
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (this.exporter is IDisposable disposableExporter)
                {
                    try
                    {
                        disposableExporter.Dispose();
                    }
                    catch (Exception e)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException("Dispose", e);
                    }
                }
            }
        }
    }
}
