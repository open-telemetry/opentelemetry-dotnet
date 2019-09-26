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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;

    /// <inheritdoc/>
    public class SpanBuilder : ISpanBuilder
    {
        private readonly SpanProcessor spanProcessor;
        private readonly TraceConfig traceConfig;
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
        private DateTimeOffset startTimestamp;

        internal SpanBuilder(string name, SpanProcessor spanProcessor, TraceConfig traceConfig)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            this.spanProcessor = spanProcessor ?? throw new ArgumentNullException(nameof(spanProcessor));
            this.traceConfig = traceConfig ?? throw new ArgumentNullException(nameof(traceConfig));
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
        public ISpanBuilder SetCreateChild(bool createChild)
        {
            if (!createChild)
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
                    throw new ArgumentException(
                        "Current Activity is not running: it has not been started or has been stopped");
                }

                this.fromActivity = currentActivity;
                this.contextSource = ContextSource.Activity;
            }
            else
            {
                this.contextSource = ContextSource.CurrentActivityParent;
            }

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
        public ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            this.recordEvents = recordEvents;
            return this;
        }

        public ISpanBuilder SetStartTimestamp(DateTimeOffset startTimestamp)
        {
            this.startTimestamp = startTimestamp;
            return this;
        }

        /// <inheritdoc/>
        public ISpan StartSpan()
        {
            var activityForSpan = this.CreateActivityForSpan(this.contextSource, this.parentSpan,
                this.parentSpanContext, this.parentActivity, this.fromActivity);

            if (this.startTimestamp == default)
            {
                this.startTimestamp = new DateTimeOffset(activityForSpan.StartTimeUtc);
            }

            bool sampledIn = MakeSamplingDecision(
                this.parentSpanContext, // it is updated in CreateActivityForSpan
                this.name,
                this.sampler,
                this.links,
                activityForSpan.TraceId,
                activityForSpan.SpanId,
                this.traceConfig);

            if (sampledIn || this.recordEvents)
            {
                activityForSpan.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }
            else
            {
                activityForSpan.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }

            var childTracestate = Tracestate.Empty;

            if (this.parentSpanContext?.Tracestate != null && this.parentSpanContext.Tracestate != Tracestate.Empty)
            {
                childTracestate = this.parentSpanContext.Tracestate;
            }

            var span = new Span(
                activityForSpan,
                childTracestate,
                this.kind,
                this.traceConfig,
                this.spanProcessor,
                this.startTimestamp,
                ownsActivity: this.contextSource != ContextSource.Activity);

            if (activityForSpan.OperationName != this.name)
            {
                span.UpdateName(this.name);
            }

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
            TraceConfig traceConfig)
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
                return traceConfig
                    .Sampler
                    .ShouldSample(parent, traceId, spanId, name, parentLinks).IsSampled;
            }

            // Parent is always different than null because otherwise we use the default sampler.
            return (parent.TraceOptions & ActivityTraceFlags.Recorded) != 0 || IsAnyParentLinkSampled(parentLinks);
        }

        private static SpanContext ParentContextFromActivity(Activity activity)
        {
            var tracestate = Tracestate.Empty;
            var tracestateBuilder = Tracestate.Builder;
            if (TracestateUtils.TryExtractTracestate(activity.TraceStateString, tracestateBuilder))
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
            Activity spanActivity = null;
            Activity originalActivity = Activity.Current;
            bool needRestoreOriginal = true;

            switch (contextSource)
            {
                case ContextSource.CurrentActivityParent:
                {
                    // Activity will figure out its parent
                    spanActivity = new Activity(this.name)
                        .SetIdFormat(ActivityIdFormat.W3C)
                        .Start();

                    // chances are, Activity.Current has span attached
                    if (CurrentSpanUtils.CurrentSpan is Span currentSpan)
                    {
                        this.parentSpanContext = currentSpan.Context;
                    }
                    else
                    {
                        this.parentSpanContext = ParentContextFromActivity(spanActivity);
                    }

                    break;
                }

                case ContextSource.ExplicitActivityParent:
                {
                    spanActivity = new Activity(this.name)
                        .SetParentId(this.parentActivity.TraceId,
                            this.parentActivity.SpanId,
                            this.parentActivity.ActivityTraceFlags)
                        .Start();
                    spanActivity.TraceStateString = this.parentActivity.TraceStateString;
                    this.parentSpanContext = ParentContextFromActivity(spanActivity);
                    break;
                }

                case ContextSource.NoParent:
                {
                    spanActivity = new Activity(this.name)
                        .SetIdFormat(ActivityIdFormat.W3C)
                        .SetParentId(" ")
                        .Start();
                    this.parentSpanContext = null;
                    break;
                }

                case ContextSource.Activity:
                {
                    this.parentSpanContext = ParentContextFromActivity(this.fromActivity);
                    spanActivity = this.fromActivity;
                    needRestoreOriginal = false;
                    break;
                }

                case ContextSource.ExplicitRemoteParent:
                {
                    spanActivity = new Activity(this.name);
                    if (this.parentSpanContext.IsValid)
                    {
                        spanActivity.SetParentId(this.parentSpanContext.TraceId,
                            this.parentSpanContext.SpanId,
                            this.parentSpanContext.TraceOptions);
                    }

                    spanActivity.SetIdFormat(ActivityIdFormat.W3C);
                    spanActivity.TraceStateString = this.parentSpanContext.Tracestate.ToString();
                    spanActivity.Start();

                    break;
                }

                case ContextSource.ExplicitSpanParent:
                {
                    spanActivity = new Activity(this.name);
                    if (this.parentSpan.Context.IsValid)
                    {
                        spanActivity.SetParentId(this.parentSpan.Context.TraceId,
                            this.parentSpan.Context.SpanId,
                            this.parentSpan.Context.TraceOptions);
                    }

                    spanActivity.SetIdFormat(ActivityIdFormat.W3C);
                    spanActivity.TraceStateString = this.parentSpan.Context.Tracestate.ToString();
                    spanActivity.Start();

                    this.parentSpanContext = this.parentSpan.Context;
                    break;
                }

                default:
                    throw new ArgumentException($"Unknown parentType {contextSource}");
            }

            if (needRestoreOriginal)
            {
                // Activity Start always puts Activity on top of Current stack
                // in OpenTelemetry we ask users to enable implicit propagation by calling WithSpan
                // it will set Current Activity and attach span to it.
                // we need to work with .NET team to allow starting Activities without updating Current
                // As a workaround here we are undoing updating Current
                Activity.Current = originalActivity;
            }

            return spanActivity;
        }
    }
}
