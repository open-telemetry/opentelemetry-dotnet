// <copyright file="SpanSdk.cs" company="OpenTelemetry Authors">
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
    internal sealed class SpanSdk : ISpan, IDisposable
    {
        internal static readonly SpanSdk Invalid = new SpanSdk();
        private static readonly ConditionalWeakTable<Activity, SpanSdk> ActivitySpanTable = new ConditionalWeakTable<Activity, SpanSdk>();

        private readonly SpanData spanData;
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

        private SpanSdk()
        {
            this.Name = string.Empty;
            this.Context = default;
            this.IsRecording = false;
        }

        private SpanSdk(
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
            if (name != null)
            {
                this.Name = name;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpan", nameof(name), "is null");
                this.Name = string.Empty;
            }

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
                spanKind,
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
                        this.SetAttribute(attribute.Key, attribute.Value);
                    }
                }

                this.spanData = new SpanData(this);
                this.spanProcessor.OnStart(this.spanData);
            }
        }

        public SpanContext Context { get; private set; }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public Status Status
        {
            get => this.StatusWithDefault;

            set
            {
                if (!value.IsValid)
                { 
                    OpenTelemetrySdkEventSource.Log.InvalidArgument("set_Status", nameof(value), "is null");
                    return;
                }

                this.status = value;
            }
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
        /// Gets the "Library Resource" (name + version) associated with the TracerSdk that produced this span.
        /// </summary>
        public Resource LibraryResource { get; }

        internal static SpanSdk Current
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null)
                {
                    return Invalid;
                }

                if (ActivitySpanTable.TryGetValue(currentActivity, out var currentSpan))
                {
                    return currentSpan;
                }

                return Invalid;
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

            if (name != null)
            {
                this.Name = name;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("UpdateName", nameof(name), "is null");
                this.Name = string.Empty;
            }
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, object value)
        {
            if (!this.IsRecording)
            {
                return;
            }

            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("SetAttribute");
                return;
            }

            object sanitizedValue = value;
            if (value == null || !this.IsAttributeValueTypeSupported(value))
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("SetAttribute", nameof(value), $"Type '{value?.GetType()}' of attribute '{key}' is not supported");
                sanitizedValue = string.Empty;
            }

            lock (this.lck)
            {
                if (this.attributes == null)
                {
                    this.attributes =
                        new EvictingQueue<KeyValuePair<string, object>>(this.tracerConfiguration.MaxNumberOfAttributes);
                }

                this.attributes.Add(new KeyValuePair<string, object>(key ?? string.Empty, sanitizedValue));
            }
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, bool value)
        {
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

                this.attributes.Add(new KeyValuePair<string, object>(key ?? string.Empty, value));
            }
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, long value)
        {
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

                this.attributes.Add(new KeyValuePair<string, object>(key ?? string.Empty, value));
            }
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, double value)
        {
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

                this.attributes.Add(new KeyValuePair<string, object>(key ?? string.Empty, value));
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
        public void AddEvent(Event addEvent)
        {
            if (addEvent == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("AddEvent", nameof(addEvent), "is null");
                return;
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
                this.spanProcessor.OnEnd(this.spanData);
            }
        }

        public void Dispose()
        {
            this.End();
        }

        internal static SpanSdk CreateFromParentSpan(
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
                return new SpanSdk(
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
            if (currentActivity == null ||
                currentActivity.IdFormat != ActivityIdFormat.W3C)
            {
                return new SpanSdk(
                    name,
                    default,
                    CreateRoot(name),
                    false,
                    spanKind,
                    spanCreationOptions,
                    sampler,
                    tracerConfiguration,
                    spanProcessor,
                    libraryResource);
            }

            return new SpanSdk(
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

        internal static SpanSdk CreateFromParentContext(
            string name,
            SpanContext parentContext,
            SpanKind spanKind,
            SpanCreationOptions spanCreationOptions,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new SpanSdk(
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

        internal static SpanSdk CreateRoot(
            string name,
            SpanKind spanKind,
            SpanCreationOptions spanCreationOptions,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            return new SpanSdk(
                name,
                default,
                CreateRoot(name),
                false,
                spanKind,
                spanCreationOptions,
                sampler,
                tracerConfiguration,
                spanProcessor,
                libraryResource);
        }

        internal static SpanSdk CreateFromActivity(
            string name,
            Activity activity,
            SpanKind spanKind,
            IEnumerable<Link> links,
            Sampler sampler,
            TracerConfiguration tracerConfiguration,
            SpanProcessor spanProcessor,
            Resource libraryResource)
        {
            var span = new SpanSdk(
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
            SpanKind spanKind,
            IDictionary<string, object> attributes,
            IEnumerable<Link> parentLinks,
            ActivityTraceId traceId,
            ActivitySpanId spanId,
            Sampler sampler)
        {
            return sampler.ShouldSample(parent, traceId, spanId, name, spanKind, attributes, parentLinks).IsSampled;
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
                if (!TracestateUtils.AppendTracestate(activity.TraceStateString, tracestate))
                {
                    activity.TraceStateString = null;
                }
            }

            return new ActivityAndTracestate(activity, tracestate);
        }

        private static ActivityAndTracestate FromParentSpan(string spanName, ISpan parentSpan)
        {
            if (parentSpan is SpanSdk parentSpanImpl && parentSpanImpl.Activity == Activity.Current)
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
            if (parentContext.IsValid)
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
                if (!TracestateUtils.AppendTracestate(activity.TraceStateString, tracestate))
                {
                    activity.TraceStateString = null;
                }
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

            return default;
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

        private bool IsAttributeValueTypeSupported(object attributeValue)
        {
            if (this.IsNumericBoolOrString(attributeValue))
            {
                return true;
            }

            // TODO add array support

            return false;
        }

        private bool IsNumericBoolOrString(object attributeValue)
        {
            return attributeValue is string
                   || attributeValue is bool
                   || attributeValue is int
                   || attributeValue is uint
                   || attributeValue is long
                   || attributeValue is ulong
                   || attributeValue is double
                   || attributeValue is sbyte
                   || attributeValue is byte
                   || attributeValue is short
                   || attributeValue is ushort
                   || attributeValue is float
                   || attributeValue is decimal;
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
            private readonly SpanSdk span;

            public ScopeInSpan(SpanSdk span)
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
