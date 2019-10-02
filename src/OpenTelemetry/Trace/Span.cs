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
    using System.Linq;
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
        private readonly TraceConfig traceConfig;
        private readonly SpanProcessor spanProcessor;
        private readonly Lazy<SpanContext> spanContext;
        private readonly DateTimeOffset startTimestamp;
        private readonly object @lock = new object();
        private EvictingQueue<KeyValuePair<string, object>> attributes;
        private EvictingQueue<Event> events;
        private EvictingQueue<Link> links;
        private Status status;
        private DateTimeOffset endTimestamp;

        internal Span(
                Activity activity,
                Tracestate tracestate,
                SpanKind spanKind,
                TraceConfig traceConfig,
                SpanProcessor spanProcessor,
                DateTimeOffset startTimestamp,
                bool ownsActivity,
                Resource libraryResource)
        {
            this.Activity = activity;
            this.spanContext = new Lazy<SpanContext>(() => new SpanContext(
                this.Activity.TraceId, 
                this.Activity.SpanId, 
                this.Activity.ActivityTraceFlags, 
                tracestate));
            this.Name = this.Activity.OperationName;
            this.traceConfig = traceConfig;
            this.spanProcessor = spanProcessor;
            this.Kind = spanKind;
            this.OwnsActivity = ownsActivity;
            this.IsRecordingEvents = this.Activity.Recorded;
            this.startTimestamp = startTimestamp;
            this.LibraryResource = libraryResource;

            if (this.IsRecordingEvents)
            {
                this.spanProcessor.OnStart(this);
            }
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
        /// Gets attributes.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes => this.attributes ?? Enumerable.Empty<KeyValuePair<string, object>>();

        /// <summary>
        /// Gets events.
        /// </summary>
        public IEnumerable<Event> Events => this.events ?? Enumerable.Empty<Event>();

        /// <summary>
        /// Gets links.
        /// </summary>
        public IEnumerable<Link> Links => this.links ?? Enumerable.Empty<Link>();

        /// <summary>
        /// Gets span start timestamp.
        /// </summary>
        public DateTimeOffset StartTimestamp => this.startTimestamp;

        /// <summary>
        /// Gets span end timestamp.
        /// </summary>
        public DateTimeOffset EndTimestamp => this.endTimestamp;

        /// <summary>
        /// Gets the span kind.
        /// </summary>
        public SpanKind? Kind { get; }

        /// <summary>
        /// Gets the "Library Resource" (name + version) associated with the Tracer that produced this span.
        /// </summary>
        public Resource LibraryResource { get; }
        
        internal bool OwnsActivity { get; }
        
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

                if (this.attributes == null)
                {
                    this.attributes = new EvictingQueue<KeyValuePair<string, object>>(this.traceConfig.MaxNumberOfAttributes);
                }

                this.attributes.AddEvent(new KeyValuePair<string, object>(keyValuePair.Key, keyValuePair.Value));
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

                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.traceConfig.MaxNumberOfEvents);
                }

                this.events.AddEvent(new Event(name, PreciseTimestamp.GetUtcNow()));
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

                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.traceConfig.MaxNumberOfEvents);
                }

                this.events.AddEvent(new Event(name, PreciseTimestamp.GetUtcNow(), eventAttributes));
            }
        }

        /// <inheritdoc/>
        public void AddEvent(Event addEvent)
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

                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.traceConfig.MaxNumberOfEvents);
                }

                this.events.AddEvent(addEvent);
            }
        }

        /// <inheritdoc/>
        public void AddLink(Link link)
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

                if (this.links == null)
                {
                    this.links = new EvictingQueue<Link>(this.traceConfig.MaxNumberOfLinks);
                }

                this.links.AddEvent(link);
            }
        }

        /// <inheritdoc/>
        public void End()
        {
            this.End(PreciseTimestamp.GetUtcNow());
        }

        public void End(DateTimeOffset endTimestamp)
        {
            this.endTimestamp = endTimestamp;
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

            this.spanProcessor.OnEnd(this);
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
    }
}
