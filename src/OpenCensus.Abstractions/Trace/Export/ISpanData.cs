// <copyright file="ISpanData.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Export
{
    using OpenCensus.Common;

    /// <summary>
    /// Span data with read-only properties.
    /// </summary>
    public interface ISpanData
    {
        /// <summary>
        /// Gets the span context.
        /// </summary>
        ISpanContext Context { get; }

        /// <summary>
        /// Gets the parent span id.
        /// </summary>
        ISpanId ParentSpanId { get; }

        /// <summary>
        /// Gets a value indicating whether span has a remote parent.
        /// </summary>
        bool? HasRemoteParent { get; }

        /// <summary>
        /// Gets the span name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the start timestamp.
        /// </summary>
        Timestamp StartTimestamp { get; }

        /// <summary>
        /// Gets the collection of attributes.
        /// </summary>
        IAttributes Attributes { get; }

        /// <summary>
        /// Gets the collection of annotations.
        /// </summary>
        ITimedEvents<IAnnotation> Annotations { get; }

        /// <summary>
        /// Gets the collection of message events.
        /// </summary>
        ITimedEvents<IMessageEvent> MessageEvents { get; }

        /// <summary>
        /// Gets the links collection.
        /// </summary>
        ILinks Links { get; }

        /// <summary>
        /// Gets the childer span count.
        /// </summary>
        int? ChildSpanCount { get; }

        /// <summary>
        /// Gets the span result status.
        /// </summary>
        Status Status { get; }

        /// <summary>
        /// Gets the span kind.
        /// </summary>
        SpanKind Kind { get; }

        /// <summary>
        /// Gets the end timestamp.
        /// </summary>
        Timestamp EndTimestamp { get; }
    }
}
