﻿// <copyright file="Span.cs" company="OpenTelemetry Authors">
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
    using System.Diagnostics;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Utils;

    /// <summary>
    /// Span implementation.
    /// </summary>
    public sealed class Span : ISpan, IElement<Span>
    {
        private readonly ITraceParams traceParams;
        private readonly IStartEndHandler startEndHandler;
        private readonly DateTimeOffset startTime;
        private readonly object @lock = new object();
        private AttributesWithCapacity attributes;
        private TraceEvents<EventWithTime<IEvent>> events;
        private TraceEvents<ILink> links;
        private Status status;
        private DateTimeOffset endTime;
        private bool hasBeenEnded;
        private bool sampleToLocalSpanStore;
        private Lazy<SpanContext> spanContext;

        private Span(
                Activity activity,
                Tracestate tracestate,
                SpanOptions options,
                string name,
                SpanKind spanKind,
                ITraceParams traceParams,
                IStartEndHandler startEndHandler,
                Timer timestampConverter,
                bool stopActivity)
        {
            this.Activity = activity;
            this.spanContext = new Lazy<SpanContext>(() => SpanContext.Create(
                this.Activity.TraceId, 
                this.Activity.SpanId, 
                this.Activity.ActivityTraceFlags, 
                tracestate));
            this.Options = options;
            this.Name = name;
            this.traceParams = traceParams ?? throw new ArgumentNullException(nameof(traceParams));
            this.startEndHandler = startEndHandler;
            this.hasBeenEnded = false;
            this.sampleToLocalSpanStore = false;
            this.Kind = spanKind;
            if (this.IsRecordingEvents)
            {
                if (timestampConverter == null)
                {
                    this.TimestampConverter = Timer.StartNew();
                    this.startTime = this.TimestampConverter.StartTime;
                }
                else
                {
                    this.TimestampConverter = timestampConverter;
                    this.startTime = this.TimestampConverter.Now;
                }
            }
            else
            {
                this.startTime = DateTimeOffset.MinValue;
                this.TimestampConverter = timestampConverter;
            }

            this.OwnsActivity = stopActivity;
        }

        public Activity Activity { get; }

        public SpanContext Context => this.spanContext.Value;

        public SpanOptions Options { get; }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public Span Next { get; set; }
        
        /// <inheritdoc/>
        public Span Previous { get; set; }

        /// <inheritdoc/>
        public Status Status
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
                if (!this.IsRecordingEvents)
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

                    this.status = value ?? throw new ArgumentNullException(nameof(value));
                }
            }
        }

        public DateTimeOffset EndTime
        {
            get
            {
                lock (this.@lock)
                {
                    return this.hasBeenEnded ? this.endTime : this.TimestampConverter.Now;
                }
            }
        }

        public TimeSpan Latency
        {
            get
            {
                lock (this.@lock)
                {
                    return this.hasBeenEnded ? this.endTime - this.startTime : this.TimestampConverter.Now - this.startTime;
                }
            }
        }

        public bool IsSampleToLocalSpanStore
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

            set
            {
                lock (this.@lock)
                {
                    this.sampleToLocalSpanStore = value;
                }
            }
        }

        public ActivitySpanId ParentSpanId => this.Activity.ParentSpanId;

        public bool HasEnded => this.hasBeenEnded;

        /// <inheritdoc/>
        public bool IsRecordingEvents => this.Options.HasFlag(SpanOptions.RecordEvents);

        /// <summary>
        /// Gets or sets span kind.
        /// </summary>
        internal SpanKind? Kind { get; set; }

        internal bool OwnsActivity { get; }

        internal Timer TimestampConverter { get; private set; }

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

        private Status StatusWithDefault => this.status ?? Status.Ok;

        /// <inheritdoc />
        public void UpdateName(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <inheritdoc/>
        public void SetAttribute(KeyValuePair<string, object> keyValuePair)
        {
            if (keyValuePair.Key == null || keyValuePair.Value == null)
            {
                throw new ArgumentNullException(nameof(keyValuePair));
            }

            if (!this.IsRecordingEvents)
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

                this.InitializedAttributes.PutAttribute(keyValuePair.Key, keyValuePair.Value);
            }
        }

        /// <inheritdoc/>
        public void AddEvent(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!this.IsRecordingEvents)
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

                this.InitializedEvents.AddEvent(new EventWithTime<IEvent>(this.TimestampConverter.Now, Event.Create(name)));
            }
        }

        /// <inheritdoc/>
        public void AddEvent(string name, IDictionary<string, object> eventAttributes)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (eventAttributes == null)
            {
                throw new ArgumentNullException(nameof(eventAttributes));
            }

            if (!this.IsRecordingEvents)
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

                this.InitializedEvents.AddEvent(new EventWithTime<IEvent>(this.TimestampConverter.Now, Event.Create(name, eventAttributes)));
            }
        }

        /// <inheritdoc/>
        public void AddEvent(IEvent addEvent)
        {
            if (addEvent == null)
            {
                throw new ArgumentNullException(nameof(addEvent));
            }

            if (!this.IsRecordingEvents)
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

                this.InitializedEvents.AddEvent(new EventWithTime<IEvent>(this.TimestampConverter.Now, addEvent));
            }
        }

        /// <inheritdoc/>
        public void AddLink(ILink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            if (!this.IsRecordingEvents)
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
        public void End()
        {
            if (this.OwnsActivity && this.Activity == Activity.Current)
            {
                // TODO log if current is not span activity
                this.Activity.Stop();
            }

            if (!this.IsRecordingEvents)
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

                this.endTime = this.TimestampConverter.Now;
                this.hasBeenEnded = true;
            }

            this.startEndHandler.OnEnd(this);
        }

        public SpanData ToSpanData()
        {
            if (!this.IsRecordingEvents)
            {
                throw new InvalidOperationException("Getting SpanData for a Span without RECORD_EVENTS option.");
            }

            var attributesSpanData = Attributes.Create(this.attributes, this.attributes?.NumberOfDroppedAttributes ?? 0);

            var annotationsSpanData = CreateTimedEvents(this.InitializedEvents, this.TimestampConverter);
            var linksSpanData = LinkList.Create(this.links?.Events, this.links?.NumberOfDroppedEvents ?? 0);

            return SpanData.Create(
                this.Context, // TODO avoid using context, use Activity instead
                this.ParentSpanId,
                Resource.Empty, // TODO: determine what to do with Resource in this context
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

        /// <inheritdoc/>
        public void SetAttribute(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!this.IsRecordingEvents)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, long value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!this.IsRecordingEvents)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, double value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!this.IsRecordingEvents)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, bool value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (!this.IsRecordingEvents)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        internal static ISpan StartSpan(
                        Activity activity,
                        Tracestate tracestate,
                        SpanOptions options,
                        string name,
                        SpanKind spanKind,
                        ITraceParams traceParams,
                        IStartEndHandler startEndHandler,
                        Timer timestampConverter,
                        bool ownsActivity = true)
        {
            var span = new Span(
               activity,
               tracestate,
               options,
               name,
               spanKind,
               traceParams,
               startEndHandler,
               timestampConverter,
               ownsActivity);

            // Call onStart here instead of calling in the constructor to make sure the span is completely
            // initialized.
            if (span.IsRecordingEvents)
            {
                startEndHandler.OnStart(span);
            }

            return span;
        }

        private static ITimedEvents<T> CreateTimedEvents<T>(TraceEvents<EventWithTime<T>> events, Timer timestampConverter)
        {
            List<ITimedEvent<T>> eventsList = null;
            int numberOfDroppedEvents = 0;
            if (events != null)
            {
                eventsList = new List<ITimedEvent<T>>(events.Events.Count);
                foreach (var networkEvent in events.Events)
                {
                    eventsList.Add(networkEvent.ToSpanDataTimedEvent(timestampConverter));
                }
            }

            return TimedEvents<T>.Create(eventsList, numberOfDroppedEvents);
        }
    }
}
