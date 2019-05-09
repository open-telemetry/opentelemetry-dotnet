// <copyright file="ISpanContext.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    /// <summary>
    /// Context associated with the span.
    /// </summary>
    public interface ISpanContext
    {
        /// <summary>
        /// Gets tracestate collection that allows different vendors to participate in a trace.
        /// </summary>
        Tracestate Tracestate { get; }

        /// <summary>
        /// Gets trace identifier.
        /// </summary>
        ITraceId TraceId { get; }

        /// <summary>
        /// Gets stan identifier.
        /// </summary>
        ISpanId SpanId { get; }

        /// <summary>
        /// Gets trace options.
        /// </summary>
        TraceOptions TraceOptions { get; }

        /// <summary>
        /// Gets a value indicating whether the span is valid.
        /// </summary>
        bool IsValid { get; }
    }
}
