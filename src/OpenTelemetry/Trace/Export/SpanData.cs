// <copyright file="SpanData.cs" company="OpenTelemetry Authors">
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
    public struct SpanData
    {
        private readonly SpanSdk span;

        internal SpanData(SpanSdk span)
        {
            this.span = span;
        }

        /// <summary>
        /// Gets span context.
        /// </summary>
        public SpanContext Context => this.span.Context;

        /// <summary>
        /// Gets span name.
        /// </summary>
        public string Name => this.span.Name;

        /// <summary>
        /// Gets span status.
        /// </summary>
        public Status Status => this.span.Status;

        /// <summary>
        /// Gets parent span id.
        /// </summary>
        public ActivitySpanId ParentSpanId => this.span.ParentSpanId;

        /// <summary>
        /// Gets attributes.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes => this.span.Attributes;

        /// <summary>
        /// Gets events.
        /// </summary>
        public IEnumerable<Event> Events => this.span.Events;

        /// <summary>
        /// Gets links.
        /// </summary>
        public IEnumerable<Link> Links => this.span.Links;

        /// <summary>
        /// Gets span start timestamp.
        /// </summary>
        public DateTimeOffset StartTimestamp => this.span.StartTimestamp;

        /// <summary>
        /// Gets span end timestamp.
        /// </summary>
        public DateTimeOffset EndTimestamp => this.span.EndTimestamp;

        /// <summary>
        /// Gets the span kind.
        /// </summary>
        public SpanKind? Kind => this.span.Kind;

        /// <summary>
        /// Gets the "Library Resource" (name + version) associated with the TracerSdk that produced this span.
        /// </summary>
        public Resource LibraryResource => this.span.LibraryResource;

        /// <summary>
        /// Compare two <see cref="SpanData"/> for equality.
        /// </summary>
        /// <param name="spanData1">First SpanData to compare.</param>
        /// <param name="spanData2">Second SpanData to compare.</param>
        public static bool operator ==(SpanData spanData1, SpanData spanData2) => spanData1.Equals(spanData2);

        /// <summary>
        /// Compare two <see cref="Status"/> for not equality.
        /// </summary>
        /// <param name="spanData1">First SpanData to compare.</param>
        /// <param name="spanData2">Second SpanData to compare.</param>
        public static bool operator !=(SpanData spanData1, SpanData spanData2) => !spanData1.Equals(spanData2);

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is SpanData))
            {
                return false;
            }

            var that = (SpanData)obj;

            return object.ReferenceEquals(this.span, that.span);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var result = 1;
            result = (31 * result) + this.span.GetHashCode();
            return result;
        }
    }
}
