﻿// <copyright file="SpanData.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Common;

    /// <inheritdoc/>
    public sealed class SpanData : ISpanData
    {
        internal SpanData(
            ISpanContext context,
            ISpanId parentSpanId,
            string name,
            Timestamp startTimestamp,
            IAttributes attributes,
            ITimedEvents<IEvent> events,
            ILinks links,
            int? childSpanCount,
            Status status,
            SpanKind kind,
            Timestamp endTimestamp)
        {
            this.Context = context ?? throw new ArgumentNullException(nameof(context));
            this.ParentSpanId = parentSpanId;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.StartTimestamp = startTimestamp ?? throw new ArgumentNullException(nameof(startTimestamp));
            this.Attributes = attributes ?? Export.Attributes.Create(new Dictionary<string, IAttributeValue>(), 0);
            this.Events = events ?? TimedEvents<IEvent>.Create(Enumerable.Empty<ITimedEvent<IEvent>>(), 0);
            this.Links = links ?? LinkList.Create(Enumerable.Empty<ILink>(), 0);
            this.ChildSpanCount = childSpanCount;
            this.Status = status;
            this.Kind = kind;
            this.EndTimestamp = endTimestamp;
        }

        /// <inheritdoc/>
        public ISpanContext Context { get; }

        /// <inheritdoc/>
        public ISpanId ParentSpanId { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public IAttributes Attributes { get; }

        /// <inheritdoc/>
        public ITimedEvents<IEvent> Events { get; }

        /// <inheritdoc/>
        public ILinks Links { get; }

        /// <inheritdoc/>
        public int? ChildSpanCount { get; }

        /// <inheritdoc/>
        public Status Status { get; }

        /// <inheritdoc/>
        public SpanKind Kind { get; }

        /// <inheritdoc/>
        public Timestamp EndTimestamp { get; }

        /// <inheritdoc/>
        public Timestamp StartTimestamp { get; }

        /// <summary>
        /// Returns a new immutable <see cref="SpanData"/>.
        /// </summary>
        /// <param name="context">The <see cref="SpanContext"/> of the <see cref="ISpan"/>.</param>
        /// <param name="parentSpanId">The parent <see cref="SpanId"/> of the <see cref="ISpan"/>. <c>null</c> if the <see cref="ISpan"/> is a root.</param>
        /// <param name="name">The name of the <see cref="ISpan"/>.</param>
        /// <param name="startTimestamp">The start <see cref="Timestamp"/> of the <see cref="ISpan"/>.</param>
        /// <param name="attributes">The <see cref="IAttributes"/> associated with the <see cref="ISpan"/>.</param>
        /// <param name="events">The <see cref="Events"/> associated with the <see cref="ISpan"/>.</param>
        /// <param name="links">The <see cref="ILinks"/> associated with the <see cref="ISpan"/>.</param>
        /// <param name="childSpanCount">The <see cref="ChildSpanCount"/> associated with the <see cref="ISpan"/>.</param>
        /// <param name="status">The <see cref="Status"/> of the <see cref="ISpan"/>.</param>
        /// <param name="kind">The <see cref="SpanKind"/> of the <see cref="ISpan"/>.</param>
        /// <param name="endTimestamp">The end <see cref="Timestamp"/> of the <see cref="ISpan"/>.</param>
        /// <returns>A new immutable <see cref="SpanData"/>.</returns>
        public static ISpanData Create(
                        ISpanContext context,
                        ISpanId parentSpanId,
                        string name,
                        Timestamp startTimestamp,
                        IAttributes attributes,
                        ITimedEvents<IEvent> events,
                        ILinks links,
                        int? childSpanCount,
                        Status status,
                        SpanKind kind,
                        Timestamp endTimestamp)
        {
            if (events == null)
            {
                events = TimedEvents<IEvent>.Create(new List<ITimedEvent<IEvent>>(), 0);
            }

            return new SpanData(
                context,
                parentSpanId,
                name,
                startTimestamp,
                attributes,
                events,
                links,
                childSpanCount,
                status,
                kind,
                endTimestamp);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "SpanData{"
                + "context=" + this.Context + ", "
                + "parentSpanId=" + this.ParentSpanId + ", "
                + "name=" + this.Name + ", "
                + "startTimestamp=" + this.StartTimestamp + ", "
                + "attributes=" + this.Attributes + ", "
                + "events=" + this.Events + ", "
                + "links=" + this.Links + ", "
                + "childSpanCount=" + this.ChildSpanCount + ", "
                + "status=" + this.Status + ", "
                + "endTimestamp=" + this.EndTimestamp
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is SpanData that)
            {
                return this.Context.Equals(that.Context)
                     && ((this.ParentSpanId == null) ? (that.ParentSpanId == null) : this.ParentSpanId.Equals(that.ParentSpanId))
                     && this.Name.Equals(that.Name)
                     && this.StartTimestamp.Equals(that.StartTimestamp)
                     && this.Attributes.Equals(that.Attributes)
                     && this.Events.Equals(that.Events)
                     && this.Links.Equals(that.Links)
                     && ((this.ChildSpanCount == null) ? (that.ChildSpanCount == null) : this.ChildSpanCount.Equals(that.ChildSpanCount))
                     && ((this.Status == null) ? (that.Status == null) : this.Status.Equals(that.Status))
                     && ((this.EndTimestamp == null) ? (that.EndTimestamp == null) : this.EndTimestamp.Equals(that.EndTimestamp));
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.Context.GetHashCode();
            h *= 1000003;
            h ^= (this.ParentSpanId == null) ? 0 : this.ParentSpanId.GetHashCode();
            h *= 1000003;
            h ^= this.Name.GetHashCode();
            h *= 1000003;
            h ^= this.StartTimestamp.GetHashCode();
            h *= 1000003;
            h ^= this.Attributes.GetHashCode();
            h *= 1000003;
            h ^= this.Events.GetHashCode();
            h *= 1000003;
            h *= 1000003;
            h ^= this.Links.GetHashCode();
            h *= 1000003;
            h ^= (this.ChildSpanCount == null) ? 0 : this.ChildSpanCount.GetHashCode();
            h *= 1000003;
            h ^= (this.Status == null) ? 0 : this.Status.GetHashCode();
            h *= 1000003;
            h ^= (this.EndTimestamp == null) ? 0 : this.EndTimestamp.GetHashCode();
            return h;
        }
    }
}
