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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Internal;
using OpenTelemetry.Utils;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Span implementation.
    /// </summary>
    public sealed class Span : ISpan, IDisposable
    {
        private static readonly ConditionalWeakTable<Activity, Span> ActivitySpanTable = new ConditionalWeakTable<Activity, Span>();

        private readonly Sampler sampler;
        private readonly TracerConfiguration tracerConfiguration;
        private readonly SpanProcessor spanProcessor;
        private readonly object lck = new object();
        private readonly bool createdFromActivity;

        private EvictingQueue<KeyValuePair<string, object>> attributes;
        private EvictingQueue<Event> events;
        private List<Link> links;
        private Status status;

        private DateTimeOffset startTimestamp;
        private DateTimeOffset endTimestamp;
        private bool hasEnded;
        private bool endOnDispose;

        private Span(
            string name,
            SpanContext parentSpanContext,
            ActivityAndTracestate activityAndTracestate,
            bool createdFromActivity,
            SpanKind spanKind,
            SpanCreationOptions spanCreationOptions,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        { 
            this.Name = name;
            this.LibraryResource = libraryResource;

            IEnumerable<Link> links = null;
            if (spanCreationOptions != null)
            {
                links = spanCreationOptions.Links ?? spanCreationOptions.LinksFactory?.Invoke();
                this.startTimestamp = spanCreationOptions.StartTimestamp;
            }

            if (this.startTimestamp == default)
            {
                this.startTimestamp = PreciseTimestamp.GetUtcNow();
            }

            this.sampler = sampler;
            this.tracerConfiguration = tracerConfiguration;
            this.spanProcessor = spanProcessor;
            this.Kind = spanKind;
            this.createdFromActivity = createdFromActivity;
            this.Activity = activityAndTracestate.Activity;
            var tracestate = activityAndTracestate.Tracestate;

            this.IsRecording = MakeSamplingDecision(
                parentSpanContext,
                name,
                spanCreationOptions?.Attributes,
                links, // we'll enumerate again, but double enumeration over small collection is cheaper than allocation
                this.Activity.TraceId,
                this.Activity.SpanId,
                this.sampler);

            this.Activity.ActivityTraceFlags =
                this.IsRecording
                ? this.Activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded
                : this.Activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;

            // this context is definitely not remote, setting isRemote to false
            this.Context = new SpanContext(this.Activity.TraceId, this.Activity.SpanId, this.Activity.ActivityTraceFlags, false, tracestate);

            if (this.IsRecording)
            {
                this.SetLinks(links);

                if (spanCreationOptions?.Attributes != null)
                {
                    foreach (var attribute in spanCreationOptions.Attributes)
                    {
                        this.SetAttribute(attribute);
                    }
                }

                this.spanProcessor.OnStart(this);
            }
        }

        public SpanContext Context { get; private set; }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public Status Status
        {
            get => this.StatusWithDefault;

            set => this.status = value.IsValid ? value : throw new ArgumentException(nameof(value));
        }

        public ActivitySpanId ParentSpanId => this.Activity.ParentSpanId;

        /// <inheritdoc/>
        public bool IsRecording { get; }

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

        internal static Span Current
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null)
                {
                    return null;
                }

                if (ActivitySpanTable.TryGetValue(currentActivity, out var currentSpan))
                {
                    return currentSpan;
                }

                return null;
            }
        }

        internal Activity Activity { get; }

        private Status StatusWithDefault => this.status.IsValid ? this.status : Status.Ok;

        /// <inheritdoc />
        public void UpdateName(string name)
        {
            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("UpdateName");
                return;
            }

            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <inheritdoc/>
        public void SetAttribute(KeyValuePair<string, object> keyValuePair)
        {
            if (keyValuePair.Key == null)
            {
                throw new ArgumentNullException(nameof(keyValuePair.Key));
            }

            if (keyValuePair.Value == null)
            {
                throw new ArgumentNullException(nameof(keyValuePair.Value));
            }

            if (!this.IsRecording)
            {
                return;
            }

            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("SetAttribute");
                return;
            }

            lock (this.lck)
            {
                if (this.attributes == null)
                {
                    this.attributes =
                        new EvictingQueue<KeyValuePair<string, object>>(this.tracerConfiguration.MaxNumberOfAttributes);
                }

                this.attributes.Add(new KeyValuePair<string, object>(keyValuePair.Key, keyValuePair.Value));
            }
        }

        /// <inheritdoc/>
        public void AddEvent(string name)
        {
            if (!this.IsRecording)
            {
                return;
            }

            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("AddEvent");
                return;
            }

            this.AddEvent(new Event(name, PreciseTimestamp.GetUtcNow()));
        }

        /// <inheritdoc/>
        public void AddEvent(string name, IDictionary<string, object> eventAttributes)
        {
            if (!this.IsRecording)
            {
                return;
            }

            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("AddEvent");
                return;
            }

            this.AddEvent(new Event(name, PreciseTimestamp.GetUtcNow(), eventAttributes));
        }

        /// <inheritdoc/>
        public void AddEvent(Event addEvent)
        {
            if (addEvent == null)
            {
                throw new ArgumentNullException(nameof(addEvent));
            }

            if (!this.IsRecording)
            {
                return;
            }

            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("AddEvent");
                return;
            }

            lock (this.lck)
            {
                if (this.events == null)
                {
                    this.events =
                        new EvictingQueue<Event>(this.tracerConfiguration.MaxNumberOfEvents);
                }

                this.events.Add(addEvent);
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
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("End");
                return;
            }

            this.hasEnded = true;
            this.endTimestamp = endTimestamp;

            if (!this.createdFromActivity)
            {
                this.Activity.SetEndTime(endTimestamp.UtcDateTime);
            }

            if (this.endOnDispose)
            {
                this.EndScope();
            }

            if (this.IsRecording)
            {
                this.spanProcessor.OnEnd(this);
            }
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, string value)
        {
            if (!this.IsRecording)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, long value)
        {
            if (!this.IsRecording)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, double value)
        {
            if (!this.IsRecording)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, bool value)
        {
            if (!this.IsRecording)
            {
                return;
            }

            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }

        public void Dispose()
        {
            this.End();
        }

        internal static Span CreateFromParentSpan(
            string name,
            ISpan parentSpan,
            SpanKind spanKind,
            SpanCreationOptions spanCreationOptions,
            Sampler sampler,
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
                    false,
                    spanKind,
                    spanCreationOptions,
                    sampler,
                    tracerConfiguration,
                    spanProcessor,
                    libraryResource);
            }

            var currentActivity = Activity.Current;
            if (currentActivity == null)
            {
                return new Span(
                    name,
                    SpanContext.BlankLocal,
                    CreateRoot(name),
                    false,
                    spanKind,
                    spanCreationOptions,
                    sampler,
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
                false,
                spanKind,
                spanCreationOptions,
                sampler,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static Span CreateFromParentContext(
            string name,
            SpanContext parentContext,
            SpanKind spanKind,
            SpanCreationOptions spanCreationOptions,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new Span(
                name,
                parentContext,
                FromParentSpanContext(name, parentContext),
                false,
                spanKind,
                spanCreationOptions,
                sampler,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static Span CreateRoot(
            string name,
            SpanKind spanKind,
            SpanCreationOptions spanCreationOptions,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new Span(
                name,
                SpanContext.BlankLocal,
                CreateRoot(name),
                false,
                spanKind,
                spanCreationOptions,
                sampler,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static Span CreateFromActivity(
            string name,
            Activity activity,
            SpanKind spanKind,
            IEnumerable<Link> links,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            var span = new Span(
                name,
                ParentContextFromActivity(activity),
                FromActivity(activity),
                true,
                spanKind,
                null,
                sampler,
                tracerConfiguration,
                spanProcessor,
                libraryResource)
            {
                startTimestamp = new DateTimeOffset(activity.StartTimeUtc),
            };

            span.SetLinks(links);
            span.BeginScope(true);
            return span;
        }

        internal IDisposable BeginScope(bool endOnDispose)
        {
            if (ActivitySpanTable.TryGetValue(this.Activity, out _))
            {
                OpenTelemetrySdkEventSource.Log.AttemptToActivateActiveSpan(this.Name);
                return this.endOnDispose ? this : NoopDisposable.Instance;
            }

            ActivitySpanTable.Add(this.Activity, this);
            Activity.Current = this.Activity;

            this.endOnDispose = endOnDispose;
            if (this.endOnDispose)
            {
                return this;
            }

            return new ScopeInSpan(this);
        }

        private static bool MakeSamplingDecision(
            SpanContext parent,
            string name,
            IDictionary<string, object> attributes,
            IEnumerable<Link> parentLinks,
            ActivityTraceId traceId,
            ActivitySpanId spanId,
            Sampler sampler)
        {
            return sampler.ShouldSample(parent, traceId, spanId, name, attributes, parentLinks).IsSampled;
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

        private static ActivityAndTracestate FromActivity(Activity activity)
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

        private void SetLinks(IEnumerable<Link> links)
        {
            if (this.IsRecording)
            {
                if (links != null)
                {
                    var parentLinks = links.ToList();
                    if (parentLinks.Count <= this.tracerConfiguration.MaxNumberOfLinks)
                    {
                        this.links = parentLinks;
                    }
                    else
                    {
                        this.links = parentLinks.GetRange(parentLinks.Count - this.tracerConfiguration.MaxNumberOfLinks,
                            this.tracerConfiguration.MaxNumberOfLinks);
                    }
                }
            }
        }

        private void EndScope()
        {
            if (this.Activity == Activity.Current)
            {
                ActivitySpanTable.Remove(this.Activity);

                // spans created from Activity do not control
                // Activity lifetime and should not change Current activity
                if (!this.createdFromActivity)
                {
                    Activity.Current = this.Activity.Parent;
                }
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.AttemptToEndScopeWhichIsNotCurrent(this.Name);
            }
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

        private sealed class ScopeInSpan : IDisposable
        {
            private readonly Span span;

            public ScopeInSpan(Span span)
            {
                this.span = span;
            }

            public void Dispose()
            {
                this.span.EndScope();
            }
        }
    }
}
