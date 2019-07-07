// <copyright file="SpanBuilder.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;

    /// <inheritdoc/>
    public class SpanBuilder : ISpanBuilder
    {
        private readonly SpanBuilderOptions options;
        private readonly string name;

        private SpanKind kind;
        private ISpan parentSpan;
        private Activity parentActivity;
        private Activity fromActivity;
        private SpanContext parentSpanContext;
        private ContextSource contextSource = ContextSource.CurrentActivityParent;
        private ISampler sampler;
        private List<ILink> links;
        private bool recordEvents;
        private Timer timestampConverter;
        private bool disableActivityCreation = false;

        internal SpanBuilder(string name, SpanBuilderOptions options)
        {
            // TODO: remove with next DiagnosticSource preview, switch to Activity setidformat
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            this.name = name ?? throw new ArgumentNullException(nameof(name));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        private enum ContextSource
        {
            CurrentActivityParent,
            Activity,
            ExplicitActivityParent,
            ExplicitSpanParent,
            ExplicitRemoteParent,
            NoParent,
        }

        /// <inheritdoc/>
        public ISpanBuilder SetSampler(ISampler sampler)
        {
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(ISpan parentSpan)
        {
            this.parentSpan = parentSpan ?? throw new ArgumentNullException(nameof(parentSpan));
            this.contextSource = ContextSource.ExplicitSpanParent;
            this.timestampConverter = ((Span)parentSpan)?.TimestampConverter;
            this.parentSpanContext = null;
            this.parentActivity = null;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(Activity parentActivity)
        {
            this.parentActivity = parentActivity ?? throw new ArgumentNullException(nameof(parentActivity));
            this.contextSource = ContextSource.ExplicitActivityParent;
            this.parentSpanContext = null;
            this.parentSpan = null;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(SpanContext remoteParent)
        {
            this.parentSpanContext = remoteParent ?? throw new ArgumentNullException(nameof(remoteParent));
            this.parentSpan = null;
            this.parentActivity = null;
            this.contextSource = ContextSource.ExplicitRemoteParent;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetNoParent()
        {
            this.contextSource = ContextSource.NoParent;
            this.parentSpanContext = null;
            this.parentSpanContext = null;
            this.parentActivity = null;
            return this;
        }

        /// <inheritdoc />
        public ISpanBuilder FromCurrentActivity()
        {
            var currentActivity = Activity.Current;

            if (currentActivity == null)
            {
                throw new ArgumentException("Current Activity cannot be null");
            }

            if (currentActivity.IdFormat != ActivityIdFormat.W3C)
            {
                throw new ArgumentException("Current Activity is not in W3C format");
            }

            if (currentActivity.StartTimeUtc == default || currentActivity.Duration != default)
            {
                throw new ArgumentException("Current Activity is not running: it has not been started or has been stopped");
            }

            this.fromActivity = currentActivity;
            this.contextSource = ContextSource.Activity;
            this.parentSpanContext = null;
            this.parentSpanContext = null;
            this.parentActivity = null;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetSpanKind(SpanKind spanKind)
        {
            this.kind = spanKind;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(SpanContext spanContext)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            return this.AddLink(Link.FromSpanContext(spanContext));
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(SpanContext spanContext, IDictionary<string, object> attributes)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            return this.AddLink(Link.FromSpanContext(spanContext, attributes));
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(ILink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            if (this.links == null)
            {
                this.links = new List<ILink>();
            }

            this.links.Add(link);

            return this;
        }

	/// <inheritdoc/>
        public ISpanBuilder AddLink(Activity activity)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            return this.AddLink(Link.FromActivity(activity));
        }

        /// <inheritdoc/>
        public ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            this.recordEvents = recordEvents;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetDisableActivity(bool disableActivity)
        {
            this.disableActivityCreation = disableActivity;
            return this;
        }

        /// <inheritdoc/>
        public ISpan StartSpan()
        {
            var activityForSpan = this.CreateActivityForSpan(this.contextSource, this.parentSpan, this.parentSpanContext, this.parentActivity, this.fromActivity);

            var activeTraceParams = this.options.TraceConfig.ActiveTraceParams;

            bool sampledIn = MakeSamplingDecision(
                this.parentSpanContext, // it is updated in CreateActivityForSpan
                this.name,
                this.sampler,
                this.links,
                activityForSpan.TraceId,
                activityForSpan.SpanId,
                activeTraceParams);

            var spanOptions = SpanOptions.None;
            if (sampledIn || this.recordEvents)
            {
                activityForSpan.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                spanOptions = SpanOptions.RecordEvents;
            }
            else
            {
                activityForSpan.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }

            var span = Span.StartSpan(
                        activityForSpan,
                        this.parentSpanContext?.Tracestate ?? Tracestate.Empty, // it is updated in CreateActivityForSpan, 
                        spanOptions,
                        this.name,
                        this.kind,
                        activeTraceParams,
                        this.options.StartEndHandler,
                        this.timestampConverter);
            LinkSpans(span, this.links);
            return span;
        }

        private static bool IsAnyParentLinkSampled(List<ILink> parentLinks)
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

        private static void LinkSpans(ISpan span, List<ILink> parentLinks)
        {
            if (parentLinks != null)
            {
                foreach (var link in parentLinks)
                {
                    span.AddLink(link);
                }
            }
        }

        private static bool MakeSamplingDecision(
            SpanContext parent,
            string name,
            ISampler sampler,
            List<ILink> parentLinks,
            ActivityTraceId traceId,
            ActivitySpanId spanId,
            ITraceParams activeTraceParams)
        {
            // If users set a specific sampler in the SpanBuilder, use it.
            if (sampler != null)
            {
                return sampler.ShouldSample(parent, traceId, spanId, name, parentLinks);
            }

            // Use the default sampler if this is a root Span or this is an entry point Span (has remote
            // parent).
            if (parent == null || !parent.IsValid)
            {
                return activeTraceParams
                    .Sampler
                    .ShouldSample(parent, traceId, spanId, name, parentLinks);
            }

            // Parent is always different than null because otherwise we use the default sampler.
            return (parent.TraceOptions & ActivityTraceFlags.Recorded) != 0 || IsAnyParentLinkSampled(parentLinks);
        }

        private static SpanContext ParentContextFromActivity(Activity activity)
        {
            var tracestate = Tracestate.Empty;
            var tracestateBuilder = Tracestate.Builder;
            if (activity.TraceStateString.TryExtractTracestate(tracestateBuilder))
            {
                tracestate = tracestateBuilder.Build();
            }

            return SpanContext.Create(
                activity.TraceId,
                activity.ParentSpanId,
                ActivityTraceFlags.Recorded,
                tracestate);
        }

        private Activity CreateActivityForSpan(ContextSource contextSource, ISpan explicitParent, SpanContext remoteParent, Activity explicitParentActivity, Activity fromActivity)
        {
            switch (contextSource)
            {
                case ContextSource.CurrentActivityParent:
                {
                    // Activity will figure out its parent
                    var activity = new Activity(this.name).Start();

                    this.parentSpanContext = ParentContextFromActivity(activity);
                    return activity;
                }

                case ContextSource.ExplicitActivityParent:
                {
                    var activity = new Activity(this.name).SetParentId(this.parentActivity.TraceId,
                        this.parentActivity.SpanId,
                        this.parentActivity.ActivityTraceFlags);
                    activity.TraceStateString = this.parentActivity.TraceStateString;

                    activity.Start();

                    this.parentSpanContext = ParentContextFromActivity(activity);
                    return activity;
                }

                case ContextSource.NoParent:
                {
                    // TODO fix after next DiagnosticSource preview comes out - this is a hack to force activity to become orphan
                    var activity = new Activity(this.name).SetParentId(" ").Start();
                    this.parentSpanContext = null;
                    return activity;
                }

                case ContextSource.Activity:
                {
                    this.parentSpanContext = ParentContextFromActivity(this.fromActivity);
                    return this.fromActivity;
                }

                case ContextSource.ExplicitRemoteParent:
                {
                    var activity = new Activity(this.name);
                    if (this.parentSpanContext.IsValid)
                    {
                        activity.SetParentId(this.parentSpanContext.TraceId,
                            this.parentSpanContext.SpanId,
                            this.parentSpanContext.TraceOptions);
                    }

                    activity.TraceStateString = this.parentSpanContext.Tracestate.ToString();
                    activity.Start();

                    return activity;
                }

                case ContextSource.ExplicitSpanParent:
                {
                    var activity = new Activity(this.name).SetParentId(this.parentSpan.Context.TraceId,
                        this.parentSpan.Context.SpanId,
                        this.parentSpan.Context.TraceOptions);

                    activity.TraceStateString = this.parentSpan.Context.Tracestate.ToString();
                    activity.Start();

                    this.parentSpanContext = this.parentSpan.Context;
                    return activity;
                }

                default:
                    throw new ArgumentException($"Unknown parentType {contextSource}");
            }
        }
    }
}
