// <copyright file="SpanSdk.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using System.Collections;
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
    internal sealed class SpanSdk : TelemetrySpan, IDisposable
    {
        internal static readonly SpanSdk Invalid = new SpanSdk();

        private static readonly ConditionalWeakTable<Activity, SpanSdk> ActivitySpanTable = new ConditionalWeakTable<Activity, SpanSdk>();
        private readonly SpanData spanData;
        private readonly Sampler sampler;
        private readonly TracerConfiguration tracerConfiguration;
        private readonly SpanProcessor spanProcessor;
        private readonly bool createdFromActivity;
        private readonly object lck = new object();
        private readonly bool isOutOfBand;
        private bool endOnDispose;
        private Status status;
        private EvictingQueue<KeyValuePair<string, object>> attributes;
        private EvictingQueue<Event> events;
        private bool hasEnded;

        internal SpanSdk(
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
            DateTimeOffset endTimestamp,
            TracerConfiguration tracerConfiguration)
        {
            this.tracerConfiguration = tracerConfiguration;
            this.IsRecording = true;
            if (name != null)
            {
                this.Name = name;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpan", nameof(name), "is null");
                this.Name = string.Empty;
            }

            this.Context = context;
            this.Kind = kind;
            this.StartTimestamp = startTimestamp;

            this.SetLinks(links);
            if (attributes != null)
            {
                foreach (var attribute in attributes)
                {
                    this.SetAttribute(attribute.Key, attribute.Value);
                }
            }

            if (events != null)
            {
                foreach (var evnt in events)
                {
                    this.AddEvent(evnt);
                }
            }

            this.Status = status;
            this.EndTimestamp = endTimestamp;
            this.LibraryResource = resource;
            this.ParentSpanId = parentSpanId;
            this.isOutOfBand = true;
            this.hasEnded = true;
        }

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
                this.StartTimestamp = spanCreationOptions.StartTimestamp;
            }

            if (this.StartTimestamp == default)
            {
                this.StartTimestamp = PreciseTimestamp.GetUtcNow();
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
                this.sampler);

            this.Activity.ActivityTraceFlags =
                this.IsRecording
                ? this.Activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded
                : this.Activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;

            // this context is definitely not remote, setting isRemote to false
            this.Context = new SpanContext(this.Activity.TraceId, this.Activity.SpanId, this.Activity.ActivityTraceFlags, false, tracestate);
            this.ParentSpanId = this.Activity.ParentSpanId;

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

            this.isOutOfBand = false;
        }

        public override SpanContext Context { get; }

        public string Name { get; private set; }

        /// <inheritdoc/>
        public override Status Status
        {
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

        public ActivitySpanId ParentSpanId { get; }

        /// <inheritdoc/>
        public override bool IsRecording { get; }

        /// <summary>
        /// Gets attributes.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes => this.attributes;

        /// <summary>
        /// Gets events.
        /// </summary>
        public IEnumerable<Event> Events => this.events;

        /// <summary>
        /// Gets links.
        /// </summary>
        public IEnumerable<Link> Links { get; private set; }

        /// <summary>
        /// Gets span start timestamp.
        /// </summary>
        public DateTimeOffset StartTimestamp { get; private set; }

        /// <summary>
        /// Gets span end timestamp.
        /// </summary>
        public DateTimeOffset EndTimestamp { get; private set; }

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

        public Status GetStatus()
        {
            return this.status;
        }

        /// <inheritdoc />
        public override void UpdateName(string name)
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
        public override void SetAttribute(string key, object value)
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
            if (value == null)
            {
                sanitizedValue = string.Empty;
            }
            else if (!this.IsAttributeValueTypeSupported(value))
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("SetAttribute", nameof(value), $"Type '{value.GetType()}' of attribute '{key}' is not supported");
                sanitizedValue = string.Empty;
            }

            lock (this.lck)
            {
                if (this.attributes == null)
                {
                    this.attributes =
                        new EvictingQueue<KeyValuePair<string, object>>(this.tracerConfiguration.MaxNumberOfAttributes);
                }

                this.AddOrReplaceAttribute(key, sanitizedValue);
            }
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, bool value)
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

                this.AddOrReplaceAttribute(key, value);
            }
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, long value)
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

                this.AddOrReplaceAttribute(key, value);
            }
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, double value)
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

                this.AddOrReplaceAttribute(key, value);
            }
        }

        /// <inheritdoc/>
        public override void AddEvent(string name)
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
        public override void AddEvent(Event addEvent)
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
        public override void End()
        {
            this.End(PreciseTimestamp.GetUtcNow());
        }

        public override void End(DateTimeOffset endTimestamp)
        {
            if (this.hasEnded)
            {
                OpenTelemetrySdkEventSource.Log.UnexpectedCallOnEndedSpan("End");
                return;
            }

            this.hasEnded = true;
            this.EndTimestamp = endTimestamp;

            if (!this.createdFromActivity)
            {
                this.Activity?.SetEndTime(endTimestamp.UtcDateTime);
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
            TelemetrySpan parentSpan,
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
            SpanCreationOptions spanCreationOptions = null;
            if (activity.Tags.Any())
            {
                spanCreationOptions = new SpanCreationOptions
                {
                    Attributes = new TagsCollection(activity.Tags),
                };
            }

            var span = new SpanSdk(
                name,
                ParentContextFromActivity(activity),
                FromActivity(activity),
                true,
                spanKind,
                spanCreationOptions,
                sampler,
                tracerConfiguration,
                spanProcessor,
                libraryResource)
            {
                StartTimestamp = new DateTimeOffset(activity.StartTimeUtc),
            };

            span.SetLinks(links);
            span.BeginScope(true);
            return span;
        }

        internal IDisposable BeginScope(bool endOnDispose)
        {
            if (this.isOutOfBand)
            {
                OpenTelemetrySdkEventSource.Log.AttemptToActivateOobSpan(this.Name);
                return NoopDisposable.Instance;
            }

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
            IEnumerable<KeyValuePair<string, object>> attributes,
            IEnumerable<Link> parentLinks,
            ActivityTraceId traceId,
            Sampler sampler)
        {
            return sampler.ShouldSample(parent, traceId, name, spanKind, attributes, parentLinks).IsSampled;
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

        private static ActivityAndTracestate FromParentSpan(string spanName, TelemetrySpan parentSpan)
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
                activity.SetParentId(
                    parentContext.TraceId,
                    parentContext.SpanId,
                    parentContext.TraceFlags);
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
                        this.Links = parentLinks;
                    }
                    else
                    {
                        this.Links = parentLinks.GetRange(
                            parentLinks.Count - this.tracerConfiguration.MaxNumberOfLinks,
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

            if (attributeValue is IEnumerable enumerable)
            {
                try
                {
                    Type entryType = null;
                    foreach (var entry in enumerable)
                    {
                        if (entryType == null)
                        {
                            entryType = entry.GetType();
                        }

                        if (!this.IsNumericBoolOrString(entry) || entryType != entry.GetType())
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }

                return true;
            }

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

        private void AddOrReplaceAttribute(string key, object value)
        {
            var attribute = this.attributes.FirstOrDefault(a => a.Key == (key ?? string.Empty));
            var newAttribute = new KeyValuePair<string, object>(key ?? string.Empty, value);
            if (attribute.Equals(default(KeyValuePair<string, object>)))
            {
                this.attributes.Add(newAttribute);
            }
            else
            {
                this.attributes.Replace(attribute, newAttribute);
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
