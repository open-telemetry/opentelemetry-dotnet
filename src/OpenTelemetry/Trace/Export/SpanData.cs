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
using System.Linq;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Trace.Export
{
    public struct SpanData
    {
        private static readonly TracerConfiguration DefaultConfiguration = new TracerConfiguration();
        private readonly SpanSdk span;

        /// <summary>
        /// Creates SpanData instance.
        /// <remarks>This constructor should be used to export out of band spans such as 
        /// compatible events created without OpenTelemetry API or in a different process</remarks>
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="context">Span context.</param>
        /// <param name="parentSpanId">Parent span Id.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="startTimestamp">Span start timestamp.</param>
        /// <param name="attributes">Span attributes.</param>
        /// <param name="events">Span events.</param>
        /// <param name="links">Span links.</param>
        /// <param name="resource">Library resource.</param>
        /// <param name="status">Span status.</param>
        /// <param name="endTimestamp">Span end timestamp.</param>
        public SpanData(
            string name,
            in SpanContext context,
            in ActivitySpanId parentSpanId,
            SpanKind kind,
            DateTimeOffset startTimestamp,
            IEnumerable<KeyValuePair<string, object>> attributes,
            IEnumerable<Event> events,
            IEnumerable<Link> links,
            Resource resource,
            Status status,
            DateTimeOffset endTimestamp)
        {
            this.span = new SpanSdk(
                name,
                context,
                parentSpanId,
                kind,
                startTimestamp,
                attributes,
                events, links,
                resource,
                status,
                endTimestamp,
                DefaultConfiguration);
        }

        /// <summary>
        /// Creates SpanData from SpanSdk.
        /// </summary>
        /// <param name="span">Span instance.</param>
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
        public Status Status => this.span.Status.IsValid ? this.span.Status : Status.Ok;

        /// <summary>
        /// Gets parent span id.
        /// </summary>
        public ActivitySpanId ParentSpanId => this.span.ParentSpanId;

        /// <summary>
        /// Gets attributes.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes => this.span.Attributes ?? Enumerable.Empty<KeyValuePair<string, object>>();

        /// <summary>
        /// Gets events.
        /// </summary>
        public IEnumerable<Event> Events => this.span.Events ?? Enumerable.Empty<Event>();

        /// <summary>
        /// Gets links.
        /// </summary>
        public IEnumerable<Link> Links => this.span.Links ?? Enumerable.Empty<Link>();

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
        public Resource LibraryResource => this.span.LibraryResource ?? Resource.Empty;

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

            if (this.span != null && that.span != null)
            {
                return object.ReferenceEquals(this.span, that.span);
            }

            return false;
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
