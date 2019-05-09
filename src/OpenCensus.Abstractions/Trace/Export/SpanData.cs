// <copyright file="SpanData.cs" company="OpenCensus Authors">
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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenCensus.Common;

    public sealed class SpanData : ISpanData
    {
        internal SpanData(
            ISpanContext context,
            ISpanId parentSpanId,
            bool? hasRemoteParent,
            string name,
            Timestamp startTimestamp,
            IAttributes attributes,
            ITimedEvents<IAnnotation> annotations,
            ITimedEvents<IMessageEvent> messageEvents,
            ILinks links,
            int? childSpanCount,
            Status status,
            SpanKind kind,
            Timestamp endTimestamp)
        {
            this.Context = context ?? throw new ArgumentNullException(nameof(context));
            this.ParentSpanId = parentSpanId;
            this.HasRemoteParent = hasRemoteParent;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.StartTimestamp = startTimestamp ?? throw new ArgumentNullException(nameof(startTimestamp));
            this.Attributes = attributes ?? Export.Attributes.Create(new Dictionary<string, IAttributeValue>(), 0);
            this.Annotations = annotations ?? TimedEvents<IAnnotation>.Create(Enumerable.Empty<ITimedEvent<IAnnotation>>(), 0);
            this.MessageEvents = messageEvents ?? TimedEvents<IMessageEvent>.Create(Enumerable.Empty<ITimedEvent<IMessageEvent>>(), 0);
            this.Links = links ?? LinkList.Create(Enumerable.Empty<ILink>(), 0);
            this.ChildSpanCount = childSpanCount;
            this.Status = status;
            this.Kind = kind;
            this.EndTimestamp = endTimestamp;
        }

        public ISpanContext Context { get; }

        public ISpanId ParentSpanId { get; }

        public bool? HasRemoteParent { get; }

        public string Name { get; }

        public Timestamp Timestamp { get; }

        public IAttributes Attributes { get; }

        public ITimedEvents<IAnnotation> Annotations { get; }

        public ITimedEvents<IMessageEvent> MessageEvents { get; }

        public ILinks Links { get; }

        public int? ChildSpanCount { get; }

        public Status Status { get; }

        public SpanKind Kind { get; }

        public Timestamp EndTimestamp { get; }

        public Timestamp StartTimestamp { get; }

        public static ISpanData Create(
                        ISpanContext context,
                        ISpanId parentSpanId,
                        bool? hasRemoteParent,
                        string name,
                        Timestamp startTimestamp,
                        IAttributes attributes,
                        ITimedEvents<IAnnotation> annotations,
                        ITimedEvents<IMessageEvent> messageOrNetworkEvents,
                        ILinks links,
                        int? childSpanCount,
                        Status status,
                        SpanKind kind,
                        Timestamp endTimestamp)
        {
            if (messageOrNetworkEvents == null)
            {
                messageOrNetworkEvents = TimedEvents<IMessageEvent>.Create(new List<ITimedEvent<IMessageEvent>>(), 0);
            }

            var messageEventsList = new List<ITimedEvent<IMessageEvent>>();
            foreach (ITimedEvent<IMessageEvent> timedEvent in messageOrNetworkEvents.Events)
            {
                messageEventsList.Add(timedEvent);
            }

            ITimedEvents<IMessageEvent> messageEvents = TimedEvents<IMessageEvent>.Create(messageEventsList, messageOrNetworkEvents.DroppedEventsCount);
            return new SpanData(
                context,
                parentSpanId,
                hasRemoteParent,
                name,
                startTimestamp,
                attributes,
                annotations,
                messageEvents,
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
                + "hasRemoteParent=" + this.HasRemoteParent + ", "
                + "name=" + this.Name + ", "
                + "startTimestamp=" + this.StartTimestamp + ", "
                + "attributes=" + this.Attributes + ", "
                + "annotations=" + this.Annotations + ", "
                + "messageEvents=" + this.MessageEvents + ", "
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
                     && this.HasRemoteParent.Equals(that.HasRemoteParent)
                     && this.Name.Equals(that.Name)
                     && this.StartTimestamp.Equals(that.StartTimestamp)
                     && this.Attributes.Equals(that.Attributes)
                     && this.Annotations.Equals(that.Annotations)
                     && this.MessageEvents.Equals(that.MessageEvents)
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
            h ^= (this.HasRemoteParent == null) ? 0 : this.HasRemoteParent.GetHashCode();
            h *= 1000003;
            h ^= this.Name.GetHashCode();
            h *= 1000003;
            h ^= this.StartTimestamp.GetHashCode();
            h *= 1000003;
            h ^= this.Attributes.GetHashCode();
            h *= 1000003;
            h ^= this.Annotations.GetHashCode();
            h *= 1000003;
            h ^= this.MessageEvents.GetHashCode();
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
