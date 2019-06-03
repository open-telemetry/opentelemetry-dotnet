// <copyright file="ISpanData.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace.Export;

    /// <summary>
    /// Span data with read-only properties.
    /// </summary>
    public interface ISpanData
    {
        /// <summary>
        /// Gets the <see cref="SpanContext"/>.
        /// </summary>
        SpanContext Context { get; }

        /// <summary>
        /// Gets the parent <see cref="ISpanId"/>.
        /// </summary>
        ISpanId ParentSpanId { get; }

        /// <summary>
        /// Gets the span name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the start <see cref="Timestamp"/>.
        /// </summary>
        Timestamp StartTimestamp { get; }

        /// <summary>
        /// Gets the collection of <see cref="IAttributes"/> objects.
        /// </summary>
        IAttributes Attributes { get; }

        /// <summary>
        /// Gets the collection of <see cref="ITimedEvents{IEvent}"/> objects.
        /// </summary>
        ITimedEvents<IEvent> Events { get; }

        /// <summary>
        /// Gets the <see cref="ILinks"/> collection.
        /// </summary>
        ILinks Links { get; }

        /// <summary>
        /// Gets the child span count.
        /// </summary>
        int? ChildSpanCount { get; }

        /// <summary>
        /// Gets the span result <see cref="OpenTelemetry.Trace.Status"/>.
        /// </summary>
        Status Status { get; }

        /// <summary>
        /// Gets the <see cref="SpanKind"/>.
        /// </summary>
        SpanKind Kind { get; }

        /// <summary>
        /// Gets the end <see cref="Timestamp"/>.
        /// </summary>
        Timestamp EndTimestamp { get; }
    }
}
