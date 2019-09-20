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
    using System.Diagnostics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;
    using OpenTelemetry.Utils;

    /// <summary>
    /// Span implementation.
    /// </summary>
    public sealed class Span : ISpan
    {
        private readonly ITraceParams traceParams;
        private readonly IStartEndHandler startEndHandler;
        private readonly Lazy<SpanContext> spanContext;
        private readonly object @lock = new object();
        private EvictingQueue<KeyValuePair<string, object>> attributes;
        private EvictingQueue<IEvent> events;
        private EvictingQueue<ILink> links;
        private Status status;

        private Span(
                Activity activity,
                Tracestate tracestate,
                SpanKind spanKind,
                ITraceParams traceParams,
                IStartEndHandler startEndHandler,
                bool ownsActivity)
        {
            this.Activity = activity;
            this.spanContext = new Lazy<SpanContext>(() => SpanContext.Create(
                this.Activity.TraceId, 
                this.Activity.SpanId, 
                this.Activity.ActivityTraceFlags, 
                tracestate));
            this.Name = this.Activity.OperationName;
            this.traceParams = traceParams ?? throw new ArgumentNullException(nameof(traceParams));
            this.startEndHandler = startEndHandler;
            this.Kind = spanKind;
            this.OwnsActivity = ownsActivity;
            this.IsRecordingEvents = this.Activity.Recorded;
        }

        public Activity Activity { get; }

        public SpanContext Context => this.spanContext.Value;

        public string Name { get; private set; }

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
                    if (this.HasEnded)
                    {
                        // logger.log(Level.FINE, "Calling setStatus() on an ended Span.");
                        return;
                    }

                    this.status = value.IsValid ? value : throw new ArgumentException(nameof(value));
                }
            }
        }

        public ActivitySpanId ParentSpanId => this.Activity.ParentSpanId;

        public bool HasEnded { get; private set; }

        /// <inheritdoc/>
        public bool IsRecordingEvents { get; }

        /// <summary>
        /// Gets or sets span kind.
        /// </summary>
        internal SpanKind? Kind { get; set; }

        internal bool OwnsActivity { get; }

        private EvictingQueue<KeyValuePair<string, object>> InitializedAttributes
        {
            get
            {
                if (this.attributes == null)
                {
                    this.attributes = new EvictingQueue<KeyValuePair<string, object>>(this.traceParams.MaxNumberOfAttributes);
                }

                return this.attributes;
            }
        }

        private EvictingQueue<IEvent> InitializedEvents
        {
            get
            {
                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<IEvent>(this.traceParams.MaxNumberOfEvents);
                }

                return this.events;
            }
        }

        private EvictingQueue<ILink> InitializedLinks
        {
            get
            {
                if (this.links == null)
                {
                    this.links = new EvictingQueue<ILink>(this.traceParams.MaxNumberOfLinks);
                }

                return this.links;
            }
        }

        private Status StatusWithDefault => this.status.IsValid ? this.status : Status.Ok;

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
                if (this.HasEnded)
                {
                    // logger.log(Level.FINE, "Calling putAttributes() on an ended Span.");
                    return;
                }

                this.InitializedAttributes.AddEvent(new KeyValuePair<string, object>(keyValuePair.Key, keyValuePair.Value));
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
                if (this.HasEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                this.InitializedEvents.AddEvent(Event.Create(name, PreciseTimestamp.GetUtcNow()));
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
                if (this.HasEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                this.InitializedEvents.AddEvent(Event.Create(name, PreciseTimestamp.GetUtcNow(), eventAttributes));
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
                if (this.HasEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                this.InitializedEvents.AddEvent(addEvent);
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
                if (this.HasEnded)
                {
                    // logger.log(Level.FINE, "Calling addLink() on an ended Span.");
                    return;
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
                if (this.HasEnded)
                {
                    // logger.log(Level.FINE, "Calling end() on an ended Span.");
                    return;
                }

                this.HasEnded = true;
            }

            this.startEndHandler.OnEnd(this);
        }

        public SpanData ToSpanData()
        {
            if (!this.IsRecordingEvents)
            {
                throw new InvalidOperationException("Getting SpanData for a Span without RECORD_EVENTS option.");
            }

            var attributesSpanData = Attributes.Create(this.attributes?.ToReadOnlyCollection(), this.attributes?.DroppedItems ?? 0);
            var eventsSpanData = TimedEvents<IEvent>.Create(this.events?.ToReadOnlyCollection(), this.events?.DroppedItems ?? 0);
            var linksSpanData = LinkList.Create(this.links?.ToReadOnlyCollection(), this.links?.DroppedItems ?? 0);

            return SpanData.Create(
                this.Context, // TODO avoid using context, use Activity instead
                this.ParentSpanId,
                Resource.Empty, // TODO: determine what to do with Resource in this context
                this.Name,
                this.Activity.StartTimeUtc,
                attributesSpanData,
                eventsSpanData,
                linksSpanData,
                null, // Not supported yet.
                this.HasEnded ? this.StatusWithDefault : new Status(),
                this.Kind ?? SpanKind.Internal,
                this.HasEnded ? (this.Activity.StartTimeUtc + this.Activity.Duration) : default);
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
                        SpanKind spanKind,
                        ITraceParams traceParams,
                        IStartEndHandler startEndHandler,
                        bool ownsActivity = true)
        {
            var span = new Span(
               activity,
               tracestate,
               spanKind,
               traceParams,
               startEndHandler,
               ownsActivity);

            // Call onStart here instead of calling in the constructor to make sure the span is completely
            // initialized.
            if (span.IsRecordingEvents)
            {
                startEndHandler.OnStart(span);
            }

            return span;
        }
    }
}
