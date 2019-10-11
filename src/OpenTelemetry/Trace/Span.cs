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
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Configuration;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;
    using OpenTelemetry.Utils;

    /// <summary>
    /// Span implementation.
    /// </summary>
    public sealed class Span : ISpan
    {
        private readonly TracerConfiguration tracerConfiguration;
        private readonly SpanProcessor spanProcessor;
        private readonly DateTimeOffset startTimestamp;
        private readonly object @lock = new object();
        private EvictingQueue<KeyValuePair<string, object>> attributes;
        private EvictingQueue<Event> events;
        private EvictingQueue<Link> links;
        private Status status;
        private DateTimeOffset endTimestamp;
        private bool hasEnded;

        private Span(
            string name,
            SpanContext parentSpanContext,
            ActivityAndTracestate activityAndTracestate,
            bool ownsActivity,
            SpanKind spanKind,
            DateTimeOffset startTimestamp,
            IEnumerable<Link> links,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            this.Name = name;
            this.LibraryResource = libraryResource;
            this.startTimestamp = startTimestamp;
            this.tracerConfiguration = tracerConfiguration;
            this.spanProcessor = spanProcessor;
            this.Kind = spanKind;
            this.OwnsActivity = ownsActivity;
            this.Activity = activityAndTracestate.Activity;

            var tracestate = activityAndTracestate.Tracestate;

            this.IsRecordingEvents = MakeSamplingDecision(
                parentSpanContext,
                name,
                null,
                links, // we'll enumerate again, but double enumeration over small collection is cheaper than allocation
                this.Activity.TraceId,
                this.Activity.SpanId,
                this.tracerConfiguration);

            if (this.IsRecordingEvents)
            {
                this.Activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        this.AddLink(link);
                    }
                }

                this.spanProcessor.OnStart(this);
            }
            else
            {
                this.Activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }

            this.Context = new SpanContext(this.Activity.TraceId, this.Activity.SpanId, this.Activity.ActivityTraceFlags, tracestate);
        }

        public SpanContext Context { get; private set; }

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
                    this.status = value.IsValid ? value : throw new ArgumentException(nameof(value));
                }
            }
        }

        public ActivitySpanId ParentSpanId => this.Activity.ParentSpanId;

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

        internal Activity Activity { get; }

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

            if (!this.IsRecordingEvents || this.hasEnded)
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.attributes == null)
                {
                    this.attributes = new EvictingQueue<KeyValuePair<string, object>>(this.tracerConfiguration.MaxNumberOfAttributes);
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

            if (!this.IsRecordingEvents || this.hasEnded)
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.tracerConfiguration.MaxNumberOfEvents);
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

            if (!this.IsRecordingEvents || this.hasEnded)
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.hasEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.tracerConfiguration.MaxNumberOfEvents);
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
                if (this.hasEnded)
                {
                    // logger.log(Level.FINE, "Calling AddEvent() on an ended Span.");
                    return;
                }

                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.tracerConfiguration.MaxNumberOfEvents);
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
                if (this.hasEnded)
                {
                    // logger.log(Level.FINE, "Calling addLink() on an ended Span.");
                    return;
                }

                if (this.links == null)
                {
                    this.links = new EvictingQueue<Link>(this.tracerConfiguration.MaxNumberOfLinks);
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
            if (this.hasEnded)
            {
                return;
            }

            this.hasEnded = true;
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

        internal static Span CreateFromParentSpan(
            string name,
            ISpan parentSpan,
            SpanKind spanKind,
            DateTimeOffset startTimestamp,
            IEnumerable<Link> links,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            if (parentSpan.Context.IsValid)
            {
                return new Span(
                    name,
                    parentSpan.Context,
                    FromParentSpan(name, parentSpan),
                    true,
                    spanKind,
                    startTimestamp,
                    links,
                    tracerConfiguration,
                    spanProcessor,
                    libraryResource);
            }

            var currentActivity = Activity.Current;
            if (currentActivity == null)
            {
                return new Span(
                    name,
                    SpanContext.Blank,
                    CreateRoot(name),
                    true,
                    spanKind,
                    startTimestamp,
                    links,
                    tracerConfiguration,
                    spanProcessor,
                    libraryResource);
            }

            return new Span(
                name,
                new SpanContext(
                    currentActivity.TraceId,
                    currentActivity.SpanId,
                    currentActivity.ActivityTraceFlags),
                FromCurrentParentActivity(name, currentActivity),
                true,
                spanKind,
                startTimestamp,
                links,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static Span CreateFromParentContext(
            string name,
            SpanContext parentContext,
            SpanKind spanKind,
            DateTimeOffset startTimestamp,
            IEnumerable<Link> links,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new Span(
                name,
                parentContext,
                FromParentSpanContext(name, parentContext),
                true,
                spanKind,
                startTimestamp,
                links,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static Span CreateRoot(
            string name,
            SpanKind spanKind,
            DateTimeOffset startTimestamp,
            IEnumerable<Link> links,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new Span(
                name,
                SpanContext.Blank,
                CreateRoot(name),
                true,
                spanKind,
                startTimestamp,
                links,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static Span CreateFromActivity(
            string name,
            Activity activity,
            SpanKind spanKind,
            IEnumerable<Link> links,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new Span(
                name,
                ParentContextFromActivity(activity),
                FromActivity(name, activity),
                false,
                spanKind,
                new DateTimeOffset(activity.StartTimeUtc),
                links,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        private static bool MakeSamplingDecision(
            SpanContext parent,
            string name,
            ISampler sampler,
            IEnumerable<Link> parentLinks,
            ActivityTraceId traceId,
            ActivitySpanId spanId,
            TracerConfiguration tracerConfiguration)
        {
            // If users set a specific sampler in the SpanBuilder, use it.
            if (sampler != null)
            {
                return sampler.ShouldSample(parent, traceId, spanId, name, parentLinks).IsSampled;
            }

            // Use the default sampler if this is a root Span or this is an entry point Span (has remote
            // parent).
            if (parent == null || !parent.IsValid)
            {
                return tracerConfiguration
                    .Sampler
                    .ShouldSample(parent, traceId, spanId, name, parentLinks).IsSampled;
            }

            // Parent is always different than null because otherwise we use the default sampler.
            return (parent.TraceOptions & ActivityTraceFlags.Recorded) != 0 || IsAnyParentLinkSampled(parentLinks);
        }

        private static bool IsAnyParentLinkSampled(IEnumerable<Link> parentLinks)
        {
            if (parentLinks != null)
            {
                foreach (var parentLink in parentLinks)
                {
                    if ((parentLink.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ActivityAndTracestate FromCurrentParentActivity(string spanName, Activity current)
        {
            var activity = new Activity(spanName);
            activity.SetIdFormat(ActivityIdFormat.W3C);

            activity.Start();
            Activity.Current = current;

            List<KeyValuePair<string, string>> tracestate = null;
            if (activity.TraceStateString != null)
            {
                tracestate = new List<KeyValuePair<string, string>>();
                TracestateUtils.AppendTracestate(activity.TraceStateString, tracestate);
            }

            return new ActivityAndTracestate(activity, tracestate);
        }

        private static ActivityAndTracestate FromParentSpan(string spanName, ISpan parentSpan)
        {
            if (parentSpan is Span parentSpanImpl && parentSpanImpl.Activity == Activity.Current)
            {
                var activity = new Activity(spanName);
                activity.SetIdFormat(ActivityIdFormat.W3C);
                activity.TraceStateString = parentSpanImpl.Activity.TraceStateString;

                var originalActivity = Activity.Current;
                activity.Start();
                Activity.Current = originalActivity;

                return new ActivityAndTracestate(activity, parentSpan.Context.Tracestate);
            }

            return FromParentSpanContext(spanName, parentSpan.Context);
        }

        private static ActivityAndTracestate FromParentSpanContext(string spanName, SpanContext parentContext)
        {
            var activity = new Activity(spanName);

            IEnumerable<KeyValuePair<string, string>> tracestate = null;
            if (parentContext != null && parentContext.IsValid)
            {
                activity.SetParentId(parentContext.TraceId,
                    parentContext.SpanId,
                    parentContext.TraceOptions);
                if (parentContext.Tracestate != null && parentContext.Tracestate.Any())
                {
                    activity.TraceStateString = TracestateUtils.GetString(parentContext.Tracestate);
                    tracestate = parentContext.Tracestate;
                }
            }

            activity.SetIdFormat(ActivityIdFormat.W3C);

            var originalActivity = Activity.Current;
            activity.Start();
            Activity.Current = originalActivity;

            return new ActivityAndTracestate(activity, tracestate);
        }

        private static ActivityAndTracestate CreateRoot(string spanName)
        {
            var activity = new Activity(spanName);
            activity.SetIdFormat(ActivityIdFormat.W3C);

            var originalActivity = Activity.Current;
            if (originalActivity != null)
            {
                activity.SetParentId(" ");
            }

            activity.Start();
            Activity.Current = originalActivity;

            return new ActivityAndTracestate(activity, null);
        }

        private static ActivityAndTracestate FromActivity(string spanName, Activity activity)
        {
            List<KeyValuePair<string, string>> tracestate = null;
            if (activity.TraceStateString != null)
            {
                tracestate = new List<KeyValuePair<string, string>>();
                TracestateUtils.AppendTracestate(activity.TraceStateString, tracestate);
            }

            return new ActivityAndTracestate(activity, tracestate);
        }

        private static SpanContext ParentContextFromActivity(Activity activity)
        {
            if (activity.TraceId != default && activity.ParentSpanId != default)
            {
                return new SpanContext(
                    activity.TraceId,
                    activity.ParentSpanId,
                    activity.ActivityTraceFlags);
            }

            return null;
        }

        private readonly struct ActivityAndTracestate
        {
            public readonly Activity Activity;
            public readonly IEnumerable<KeyValuePair<string, string>> Tracestate;

            public ActivityAndTracestate(Activity activity, IEnumerable<KeyValuePair<string, string>> tracestate)
            {
                this.Activity = activity;
                this.Tracestate = tracestate;
            }
        }
    }
}
