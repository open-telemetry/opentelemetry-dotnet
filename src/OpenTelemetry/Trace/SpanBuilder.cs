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

using OpenTelemetry.Context;

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;

    /// <inheritdoc/>
    public class SpanBuilder : SpanBuilderBase
    {
        private SpanBuilder(string name, SpanKind kind, SpanBuilderOptions options, SpanContext parentContext = null, ISpan parent = null, Activity activity = null, bool createSpanAsChildOfActivity = true) : base(kind)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Parent = parent;
            this.ParentSpanContext = parentContext;
            this.Activity = activity;
            this.CreateSpanAsChildOfActivity = createSpanAsChildOfActivity;
            this.Options = options;

            // TODO find good place for config - it will go away with Activity.SetIdFormat in the next .NET preview
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        private SpanBuilderOptions Options { get; set; }

        private string Name { get; set; }

        private ISpan Parent { get; set; }

        private SpanContext ParentSpanContext { get; set; }

        private Activity Activity { get; set; }

        private bool CreateSpanAsChildOfActivity { get; set; }

        private ISampler Sampler { get; set; }

        private IEnumerable<ISpan> ParentLinks { get; set; } = Enumerable.Empty<ISpan>();

        private bool RecordEvents { get; set; }

        /// <inheritdoc/>
        public override ISpan StartSpan()
        {
            SpanContext parentContext = null;
            Activity finalActivityForSpan = null;
            Timer finalTimestampConverter = null;

            if (this.ParentSpanContext != null)
            {
                parentContext = this.ParentSpanContext;
                finalActivityForSpan = this.StartActivityFromSpanContext(parentContext);
            }
            else if (this.Parent != null)
            {
                parentContext = this.Parent.Context;
                finalTimestampConverter = ((Span)this.Parent).TimestampConverter;
                finalActivityForSpan = this.StartActivityFromSpanContext(parentContext);
            }
            else if (this.Activity != null)
            {
                if (this.CreateSpanAsChildOfActivity)
                {
                    // activity.SetIdFormat(W3C);
                    finalActivityForSpan = new Activity(this.Name)
                        .SetParentId(this.Activity.TraceId, this.Activity.SpanId, this.Activity.ActivityTraceFlags)
                        .Start();
                }
                else
                {
                    finalActivityForSpan = this.Activity;
                }
            }
            else if (Activity.Current != null)
            {
                finalActivityForSpan = this.CreateSpanAsChildOfActivity ? new Activity(this.Name).Start() : Activity.Current;
            }

            if (finalActivityForSpan == null)
            {
                finalActivityForSpan = new Activity(this.Name).Start();

                // TODO SetIdFormat();
            }

            if (parentContext == null && finalActivityForSpan.ParentSpanId != default)
            {
                parentContext = SpanContext.Create(finalActivityForSpan.TraceId, finalActivityForSpan.ParentSpanId,
                    finalActivityForSpan.ActivityTraceFlags, /*TODO*/ Tracestate.Empty);
            }

            return this.StartSpanInternal(
                parentContext,
                this.Name,
                this.Sampler,
                this.ParentLinks,
                this.RecordEvents,
                finalTimestampConverter,
                finalActivityForSpan);
        }

        /// <inheritdoc/>
        public override ISpanBuilder SetSampler(ISampler sampler)
        {
            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <inheritdoc/>
        public override ISpanBuilder SetParentLinks(IEnumerable<Activity> parentLinks)
        {
            throw new NotImplementedException();
        }

        public override ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks)
        {
            this.ParentLinks = parentLinks ?? throw new ArgumentNullException(nameof(parentLinks));
            return this;
        }

        /// <inheritdoc/>
        public override ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            this.RecordEvents = recordEvents;
            return this;
        }

        internal static ISpanBuilder Create(string name, SpanKind kind, ISpan parent, SpanBuilderOptions options)
        {
            return new SpanBuilder(name, kind, options, null, parent, null, true);
        }

        internal static ISpanBuilder Create(string name, SpanKind kind, SpanContext parentContext, SpanBuilderOptions options)
        {
            return new SpanBuilder(name, kind, options, parentContext, null, null, true);
        }

        internal static ISpanBuilder Create(string name, SpanKind kind, Activity activity, bool asChildOfActivity, SpanBuilderOptions options)
        {
            return new SpanBuilder(name, kind, options, null, null, activity, asChildOfActivity);
        }

        private static bool IsAnyParentLinkSampled(IEnumerable<ISpan> parentLinks)
        {
            foreach (var parentLink in parentLinks)
            {
                if ((parentLink.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0) // TODO
                {
                    return true;
                }
            }

            return false;
        }

        private static void LinkSpans(ISpan span, IEnumerable<ISpan> parentLinks)
        {
            if (parentLinks.Any())
            {
                var childLink = Link.FromSpanContext(span.Context);
                foreach (var linkedSpan in parentLinks)
                {
                    linkedSpan.AddLink(childLink);
                    span.AddLink(Link.FromSpanContext(linkedSpan.Context));
                }
            }
        }

        private static bool MakeSamplingDecision(
            SpanContext parent,
            string name,
            ISampler sampler,
            IEnumerable<ISpan> parentLinks,
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
            // TODO extension method
            return (parent.TraceOptions & ActivityTraceFlags.Recorded) != 0 || IsAnyParentLinkSampled(parentLinks);
        }

        private ISpan StartSpanInternal(
                     SpanContext parent,
                     string name,
                     ISampler sampler,
                     IEnumerable<ISpan> parentLinks,
                     bool recordEvents,
                     Timer timestampConverter,
                     Activity activityForSpan)
        {
            var activeTraceParams = this.Options.TraceConfig.ActiveTraceParams;

            bool sampledIn = MakeSamplingDecision(
                parent,
                name,
                sampler,
                parentLinks,
                activityForSpan.TraceId,
                activityForSpan.SpanId,
                activeTraceParams);

            var spanOptions = SpanOptions.None;

            if (sampledIn || recordEvents)
            {
                activityForSpan.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                spanOptions = SpanOptions.RecordEvents;
            }
            else
            {
                activityForSpan.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }

            var span = Span.StartSpan(
                        SpanContext.Create(activityForSpan.TraceId, activityForSpan.SpanId, activityForSpan.ActivityTraceFlags, parent?.Tracestate ?? Tracestate.Empty),
                        spanOptions,
                        name,
                        this.Kind,
                        activityForSpan.ParentSpanId,
                        activeTraceParams,
                        this.Options.StartEndHandler,
                        timestampConverter,
                        activityForSpan);
            LinkSpans(span, parentLinks);
            return span;
        }

        private Activity StartActivityFromSpanContext(SpanContext context)
        {
            var activity = new Activity(this.Name);
            //activity.SetIdFormat(W3C);

            if (context.IsValid)
            {
                activity.SetParentId(context.TraceId, context.SpanId, context.TraceOptions);
                // TODO tracestate
            }

            return activity.Start();
        }
    }
}
