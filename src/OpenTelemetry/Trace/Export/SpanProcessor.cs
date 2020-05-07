// <copyright file="SpanProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Trace.Export
{
    /// <summary>
    /// Span processor base class.
    /// </summary>
    public abstract class SpanProcessor
    {
        /// <summary>
        /// Span start hook. Only invoked if <see cref="TelemetrySpan.IsRecording"/> is true.
        /// This method is called synchronously on the thread that started the span.
        /// </summary>
        /// <param name="span">Instance of span to process.</param>
        public abstract void OnStart(SpanData span);

        /// <summary>
        /// Span end hook. Only invoked if <see cref="TelemetrySpan.IsRecording"/> is true.
        /// This method is called synchronously on the execution thread.
        /// </summary>
        /// <param name="span">Instance of Span to process.</param>
        public abstract void OnEnd(SpanData span);

        /// <summary>
        /// Shuts down span processor asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns <see cref="Task"/>.</returns>
        public abstract Task ShutdownAsync(CancellationToken cancellationToken);
    }
}
