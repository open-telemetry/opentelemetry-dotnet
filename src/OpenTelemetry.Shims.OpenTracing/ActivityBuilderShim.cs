﻿// <copyright file="ActivityBuilderShim.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using global::OpenTracing;
using OpenTelemetry.Trace;
using SpanCreationOptions = OpenTelemetry.Trace.SpanCreationOptions;

namespace OpenTelemetry.Shims.OpenTracing
{
    /// <summary>
    /// Adapts OpenTracing ISpanBuilder to an underlying OpenTelemetry ISpanBuilder.
    /// </summary>
    /// <remarks>Instances of this class are not thread-safe.</remarks>
    /// <seealso cref="ISpanBuilder" />
    public sealed class ActivityBuilderShim : ISpanBuilder
    {
        /// <summary>
        /// The tracer.
        /// </summary>
        private readonly ActivitySource activitySource;

        /// <summary>
        /// The span name.
        /// </summary>
        private readonly string activityName;

        /// <summary>
        /// The OpenTelemetry links. These correspond loosely to OpenTracing references.
        /// </summary>
        private readonly List<ActivityLink> links = new List<ActivityLink>();

        /// <summary>
        /// The OpenTelemetry attributes. These correspond to OpenTracing Tags.
        /// </summary>
        private readonly List<KeyValuePair<string, object>> attributes = new List<KeyValuePair<string, object>>();

        /// <summary>
        /// The set of operation names for System.Diagnostics.Activity based automatic instrumentations that indicate a root span.
        /// </summary>
        private readonly IList<string> rootOperationNamesForActivityBasedAutoInstrumentations = new List<string>
        {
            "Microsoft.AspNetCore.Hosting.HttpRequestIn",
        };

        /// <summary>
        /// The parent as an TelemetrySpan, if any.
        /// </summary>
        private Activity parentActivity;

        /// <summary>
        /// The parent as an SpanContext, if any.
        /// </summary>
        private ActivityContext parentActivityContext;

        /// <summary>
        /// The explicit start time, if any.
        /// </summary>
        private DateTimeOffset? explicitStartTime;

        private bool ignoreActiveActivity;

        private ActivityKind activityKind;

        private bool error;

        public ActivityBuilderShim(ActivitySource activitySource, string activityName, IList<string> rootOperationNamesForActivityBasedAutoInstrumentations = null)
        {
            this.activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
            this.activityName = activityName ?? throw new ArgumentNullException(nameof(activityName));
            this.ScopeManager = new ScopeManagerShim(this.activitySource);
            this.rootOperationNamesForActivityBasedAutoInstrumentations = rootOperationNamesForActivityBasedAutoInstrumentations ?? this.rootOperationNamesForActivityBasedAutoInstrumentations;
        }

        private global::OpenTracing.IScopeManager ScopeManager { get; }

        private bool ParentSet => this.parentActivity != null || this.parentActivityContext.IsValid();

        /// <inheritdoc/>
        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            if (parent == null)
            {
                return this;
            }

            return this.AddReference(global::OpenTracing.References.ChildOf, parent);
        }

        /// <inheritdoc/>
        public ISpanBuilder AsChildOf(ISpan parent)
        {
            if (parent == null)
            {
                return this;
            }

            if (!this.ParentSet)
            {
                this.parentActivity = GetOpenTelemetrySpan(parent);
                return this;
            }

            return this.AsChildOf(parent.Context);
        }

        /// <inheritdoc/>
        public ISpanBuilder AddReference(string referenceType, ISpanContext referencedContext)
        {
            if (referencedContext == null)
            {
                return this;
            }

            if (referenceType is null)
            {
                throw new ArgumentNullException(nameof(referenceType));
            }

            // TODO There is no relation between OpenTracing.References (referenceType) and OpenTelemetry Link
            var actualContext = GetOpenTelemetrySpanContext(referencedContext);
            if (!this.ParentSet)
            {
                this.parentActivityContext = actualContext;
                return this;
            }
            else
            {
                this.links.Add(new ActivityLink(actualContext));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder IgnoreActiveSpan()
        {
            Activity.Current = null;
            this.ignoreActiveActivity = true;
            return this;
        }

        /// <inheritdoc/>
        public ISpan Start()
        {
            Activity activity = null;
            var startTimestamp = this.explicitStartTime ?? default;

            // If specified, this takes precedence.
            if (this.ignoreActiveActivity)
            {
                activity = this.activitySource.StartActivity(this.activityName, this.activityKind, null, null, this.links, startTimestamp);
            }
            else if (this.parentActivity != null || this.parentActivityContext.IsValid())
            {
                activity = this.activitySource.StartActivity(this.activityName, this.activityKind, this.parentActivityContext, null, this.links, startTimestamp);
            }
            else if (this.parentActivity == null && !this.parentActivityContext.IsValid())
            {
                // We need to know if we should inherit an existing Activity-based context or start a new one.
                if (Activity.Current != null && Activity.Current.IdFormat == System.Diagnostics.ActivityIdFormat.W3C)
                {
                    var currentActivity = Activity.Current;
                    if (this.rootOperationNamesForActivityBasedAutoInstrumentations.Contains(currentActivity.OperationName))
                    {
                        activity = this.activitySource.StartActivity(this.activityName, this.activityKind, null, null, this.links, startTimestamp);

                        // activity = Activity.Current;
                    }
                }
            }

            if (activity == null)
            {
                activity = this.activitySource.StartActivity(this.activityName, this.activityKind, null, null, this.links, startTimestamp);
            }

            foreach (var kvp in this.attributes)
            {
                activity.AddTag(kvp.Key, kvp.Value.ToString());
            }

            if (this.error)
            {
                activity.SetStatus(Status.Unknown);
            }

            return new ActivityShim(activity);
        }

        /// <inheritdoc/>
        public IScope StartActive() => this.StartActive(true);

        /// <inheritdoc/>
        public IScope StartActive(bool finishSpanOnDispose)
        {
            var span = this.Start();
            return this.ScopeManager.Activate(span, finishSpanOnDispose);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithStartTimestamp(DateTimeOffset timestamp)
        {
            this.explicitStartTime = timestamp;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(string key, string value)
        {
            // see https://opentracing.io/specification/conventions/ for special key handling.
            if (global::OpenTracing.Tag.Tags.SpanKind.Key.Equals(key))
            {
                switch (value)
                {
                    case global::OpenTracing.Tag.Tags.SpanKindClient:
                        this.activityKind = ActivityKind.Client;
                        break;
                    case global::OpenTracing.Tag.Tags.SpanKindServer:
                        this.activityKind = ActivityKind.Server;
                        break;
                    case global::OpenTracing.Tag.Tags.SpanKindProducer:
                        this.activityKind = ActivityKind.Producer;
                        break;
                    case global::OpenTracing.Tag.Tags.SpanKindConsumer:
                        this.activityKind = ActivityKind.Consumer;
                        break;
                    default:
                        this.activityKind = ActivityKind.Internal;
                        break;
                }
            }
            else if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key) && bool.TryParse(value, out var booleanValue))
            {
                this.error = booleanValue;
            }
            else
            {
                // Keys must be non-null.
                // Null values => string.Empty.
                if (key != null)
                {
                    this.attributes.Add(new KeyValuePair<string, object>(key, value ?? string.Empty));
                }
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(string key, bool value)
        {
            if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key))
            {
                this.error = value;
            }
            else
            {
                this.attributes.Add(new KeyValuePair<string, object>(key, value));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(string key, int value)
        {
            this.attributes.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(string key, double value)
        {
            this.attributes.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.BooleanTag tag, bool value)
        {
            return this.WithTag(tag.Key, value);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.IntOrStringTag tag, string value)
        {
            if (int.TryParse(value, out var result))
            {
                return this.WithTag(tag.Key, result);
            }

            return this.WithTag(tag.Key, value);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.IntTag tag, int value)
        {
            return this.WithTag(tag.Key, value);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.StringTag tag, string value)
        {
            return this.WithTag(tag.Key, value);
        }

        /// <summary>
        /// Gets an implementation of OpenTelemetry TelemetrySpan from the OpenTracing ISpan.
        /// </summary>
        /// <param name="span">The span.</param>
        /// <returns>an implementation of OpenTelemetry TelemetrySpan.</returns>
        /// <exception cref="ArgumentException">span is not a valid SpanShim object.</exception>
        private static Activity GetOpenTelemetrySpan(ISpan span)
        {
            if (!(span is ActivityShim shim))
            {
                throw new ArgumentException("span is not a valid SpanShim object");
            }

            return shim.ActivityObj;
        }

        /// <summary>
        /// Gets the OpenTelemetry SpanContext.
        /// </summary>
        /// <param name="spanContext">The span context.</param>
        /// <returns>the OpenTelemetry SpanContext.</returns>
        /// <exception cref="ArgumentException">context is not a valid SpanContextShim object.</exception>
        private static ActivityContext GetOpenTelemetrySpanContext(ISpanContext spanContext)
        {
            if (!(spanContext is ActivityContextShim shim))
            {
                throw new ArgumentException("context is not a valid SpanContextShim object");
            }

            return shim.Context;
        }
    }
}
