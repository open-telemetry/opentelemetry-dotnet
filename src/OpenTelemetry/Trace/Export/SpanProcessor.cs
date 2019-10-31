// <copyright file="SpanProcessor.cs" company="OpenTelemetry Authors">
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
        /// Span start hook.
        /// </summary>
        /// <param name="span">Instance of span to process.</param>
        public abstract void OnStart(Span span);
        
        /// <summary>
        /// Span end hook.
        /// </summary>
        /// <param name="span">Instance of Span to process.</param>
        public abstract void OnEnd(Span span);

        /// <summary>
        /// Shuts down span processor asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public abstract Task ShutdownAsync(CancellationToken cancellationToken);
    }
}
