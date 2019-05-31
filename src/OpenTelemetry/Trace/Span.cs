// <copyright file="Span.cs" company="OpenTelemetry Authors">
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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Utils;

    /// <inheritdoc/>
    public sealed class Span : SpanBase
    {
        private readonly ISpanId parentSpanId;
        private readonly bool? hasRemoteParent;
        private readonly ITraceParams traceParams;
        private readonly IStartEndHandler startEndHandler;
        private readonly Timer timestampConverter;
        private readonly DateTimeOffset startTime;
        private readonly object @lock = new object();
        private AttributesWithCapacity attributes;
        private TraceEvents<EventWithTime<IEvent>> events;
        private TraceEvents<ILink> links;
        private Status status;
        private DateTimeOffset endTime;
        private bool hasBeenEnded;
        private bool sampleToLocalSpanStore;

        private Span(
                ISpanContext context,
                SpanOptions options,
                string name,
                ISpanId parentSpanId,
                bool? hasRemoteParent,
                ITraceParams traceParams,
                IStartEndHandler startEndHandler,
                Timer timestampConverter)
            : base(context, options)
        {
            this.parentSpanId = parentSpanId;
            this.hasRemoteParent = hasRemoteParent;
            this.Name = name;
            this.traceParams = traceParams ?? throw new ArgumentNullException(nameof(traceParams));
            this.startEndHandler = startEndHandler;
            this.hasBeenEnded = false;
            this.sampleToLocalSpanStore = false;
            if (options.HasFlag(SpanOptions.RecordEvents))
            {
                if (timestampConverter == null)
                {
                    this.timestampConverter = Timer.StartNew();
                    this.startTime = this.timestampConverter.StartTime;
                }
                else
                {
                    this.timestampConverter = timestampConverter;
                    this.startTime = this.timestampConverter.Now;
                }
            }
            else
            {
                this.startTime = DateTimeOffset.MinValue;
                this.timestampConverter = timestampConverter;
            }
        }

        /// <inheritdoc/>
        public override string Name { get; protected set; }

        /// <inheritdoc/>
        public override Status Status
        {
            get
            {
                lock (this.@lock)
                {
                    return this.StatusWithDefault;
                }
            }

            set
            {
                if (!this.Options.HasFlag(SpanOptions.RecordEvents))
                {
                    return;
                }

                lock (this.@lock)
                {
                    if (this.hasBeenEnded)
                    {
                        // logger.log(Level.FINE, "Calling setStatus() on an ended Span.");
                        return;
                    }

                    this.status = value;
                }
            }
        }

        /// <inheritdoc/>
        public override SpanKind? Kind
        {
            get;

            set; // TODO: do we need to notify when attempt to set on already closed Span?
        }

        /// <inheritdoc/>
        public override DateTimeOffset EndTime
        {
            get
            {
                lock (this.@lock)
                {
                    return this.hasBeenEnded ? this.endTime : this.timestampConverter.Now;
                }
            }
        }

        /// <inheritdoc/>
        public override TimeSpan Latency
        {
            get
            {
                lock (this.@lock)
                {
                    return this.hasBeenEnded ? this.endTime - this.startTime : this.timestampConverter.Now - this.startTime;
                }
            }
        }

        /// <inheritdoc/>
        public override bool IsSampleToLocalSpanStore
        {
            get
            {
                lock (this.@lock)
                {
                    if (!this.hasBeenEnded)
                    {
                        throw new InvalidOperationException("Running span does not have the SampleToLocalSpanStore set.");
                    }

                    return this.sampleToLocalSpanStore;
                }
            }
        }

        /// <inheritdoc/>
        public override ISpanId ParentSpanId
        {
            get
            {
                return this.parentSpanId;
            }
        }

        /// <inheritdoc/>
        public override bool HasEnded
        {
            get
            {
                return this.hasBeenEnded;
            }
        }

        internal Timer TimestampConverter
        {
            get
            {
                return this.timestampConverter;
            }
        }

        private AttributesWithCapacity InitializedAttributes
        {
            get
            {
                if (this.attributes == null)
                {
                    this.attributes = new AttributesWithCapacity(this.traceParams.MaxNumberOfAttributes);
                }

                return this.attributes;
            }
        }

        private TraceEvents<EventWithTime<IEvent>> InitializedEvents
        {
            get
            {
                if (this.events == null)
                {
                    this.events =
                        new TraceEvents<EventWithTime<IEvent>>(this.traceParams.MaxNumberOfEvents);
                }

                return this.events;
            }
        }

        private TraceEvents<ILink> InitializedLinks
        {
            get
            {
                if (this.links == null)
                {
                    this.links = new TraceEvents<ILink>(this.traceParams.MaxNumberOfLinks);
                }

                return this.links;
            }
        }

        private Status StatusWithDefault
        {
            get
            {
                return this.status ?? Trace.Status.Ok;
            }
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, IAttributeValue value)
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasBeenEnded)
                {
                    // logger.log(Level.FINE, "Calling putAttributes() on an ended Span.");
                    return;
                }

                this.InitializedAttributes.PutAttribute(key, value);
            }
        }

        /// <inheritdoc/>
        public override void SetAttributes(IDictionary<string, IAttributeValue> attributes)
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasBeenEnded)
                {
                    // logger.log(Level.FINE, "Calling putAttributes() on an ended Span.");
                    return;
                }

                this.InitializedAttributes.PutAttributes(attributes);
            }
        }

        /// <inheritdoc/>
        public override void AddEvent(string name, IDictionary<string, IAttributeValue> attributes)
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasBeenEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                this.InitializedEvents.AddEvent(new EventWithTime<IEvent>(this.timestampConverter.Now, Event.Create(name, attributes)));
            }
        }

        /// <inheritdoc/>
        public override void AddEvent(IEvent addEvent)
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasBeenEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                if (addEvent == null)
                {
                    throw new ArgumentNullException(nameof(addEvent));
                }

                this.InitializedEvents.AddEvent(new EventWithTime<IEvent>(this.timestampConverter.Now, addEvent));
            }
        }

        /// <inheritdoc/>
        public override void AddLink(ILink link)
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasBeenEnded)
                {
                    // logger.log(Level.FINE, "Calling addLink() on an ended Span.");
                    return;
                }

                if (link == null)
                {
                    throw new ArgumentNullException(nameof(link));
                }

                this.InitializedLinks.AddEvent(link);
            }
        }

        /// <inheritdoc/>
        public override void End(EndSpanOptions options)
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasBeenEnded)
                {
                    // logger.log(Level.FINE, "Calling end() on an ended Span.");
                    return;
                }

                if (options.Status != null)
                {
                    this.status = options.Status;
                }

                this.sampleToLocalSpanStore = options.SampleToLocalSpanStore;
                this.endTime = this.timestampConverter.Now;
                this.hasBeenEnded = true;
            }

            this.startEndHandler.OnEnd(this);
        }

        /// <inheritdoc/>
        public override ISpanData ToSpanData()
        {
            if (!this.Options.HasFlag(SpanOptions.RecordEvents))
            {
                throw new InvalidOperationException("Getting SpanData for a Span without RECORD_EVENTS option.");
            }

            Attributes attributesSpanData = this.attributes == null ? Attributes.Create(new Dictionary<string, IAttributeValue>(), 0)
                        : Attributes.Create(this.attributes, this.attributes.NumberOfDroppedAttributes);

            ITimedEvents<IEvent> annotationsSpanData = CreateTimedEvents(this.InitializedEvents, this.timestampConverter);
            LinkList linksSpanData = this.links == null ? LinkList.Create(new List<ILink>(), 0) : LinkList.Create(this.links.Events, this.links.NumberOfDroppedEvents);

            return SpanData.Create(
                this.Context,
                this.parentSpanId,
                this.hasRemoteParent,
                this.Name,
                Timestamp.FromDateTimeOffset(this.startTime),
                attributesSpanData,
                annotationsSpanData,
                linksSpanData,
                null, // Not supported yet.
                this.hasBeenEnded ? this.StatusWithDefault : null,
                this.Kind ?? SpanKind.Internal,
                this.hasBeenEnded ? Timestamp.FromDateTimeOffset(this.endTime) : null);
        }

        internal static ISpan StartSpan(
                        ISpanContext context,
                        SpanOptions options,
                        string name,
                        ISpanId parentSpanId,
                        bool? hasRemoteParent,
                        ITraceParams traceParams,
                        IStartEndHandler startEndHandler,
                        Timer timestampConverter)
        {
            var span = new Span(
               context,
               options,
               name,
               parentSpanId,
               hasRemoteParent,
               traceParams,
               startEndHandler,
               timestampConverter);

            // Call onStart here instead of calling in the constructor to make sure the span is completely
            // initialized.
            if (span.Options.HasFlag(SpanOptions.RecordEvents))
            {
                startEndHandler.OnStart(span);
            }

            return span;
        }

        private static ITimedEvents<T> CreateTimedEvents<T>(TraceEvents<EventWithTime<T>> events, Timer timestampConverter)
        {
            if (events == null)
            {
                IEnumerable<ITimedEvent<T>> empty = new ITimedEvent<T>[0];
                return TimedEvents<T>.Create(empty, 0);
            }

            var eventsList = new List<ITimedEvent<T>>(events.Events.Count);
            foreach (EventWithTime<T> networkEvent in events.Events)
            {
                eventsList.Add(networkEvent.ToSpanDataTimedEvent(timestampConverter));
            }

            return TimedEvents<T>.Create(eventsList, events.NumberOfDroppedEvents);
        }
    }
}
