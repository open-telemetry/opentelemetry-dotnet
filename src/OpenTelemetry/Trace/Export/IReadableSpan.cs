// <copyright file="IReadableSpan.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace.Export
{
    public interface IReadableSpan
    {
        /// <summary>
        /// Gets span context.
        /// </summary>
        SpanContext Context { get; }

        /// <summary>
        /// Gets span name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets span status.
        /// </summary>
        Status Status { get; }

        /// <summary>
        /// Gets parent span id.
        /// </summary>
        ActivitySpanId ParentSpanId { get; }

        /// <summary>
        /// Gets attributes.
        /// </summary>
        IEnumerable<KeyValuePair<string, object>> Attributes { get; }

        /// <summary>
        /// Gets events.
        /// </summary>
        IEnumerable<Event> Events { get; }

        /// <summary>
        /// Gets links.
        /// </summary>
        IEnumerable<Link> Links { get; }

        /// <summary>
        /// Gets span start timestamp.
        /// </summary>
        DateTimeOffset StartTimestamp { get; }

        /// <summary>
        /// Gets span end timestamp.
        /// </summary>
        DateTimeOffset EndTimestamp { get; }

        /// <summary>
        /// Gets the span kind.
        /// </summary>
        SpanKind? Kind { get; }

        /// <summary>
        /// Gets the "Library Resource" (name + version) associated with the TracerSdk that produced this span.
        /// </summary>
        Resource LibraryResource { get; }
    }
}
