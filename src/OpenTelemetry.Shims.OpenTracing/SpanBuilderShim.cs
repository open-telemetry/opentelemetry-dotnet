// <copyright file="SpanBuilderShim.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing
{
    /// <summary>
    /// Adapts OpenTracing ISpanBuilder to an underlying OpenTelemetry ISpanBuilder.
    /// </summary>
    /// <remarks>Instances of this class are not thread-safe.</remarks>
    /// <seealso cref="ISpanBuilder" />
    internal sealed class SpanBuilderShim : ISpanBuilder
    {
        /// <summary>
        /// The tracer.
        /// </summary>
        private readonly Tracer tracer;

        /// <summary>
        /// The span name.
        /// </summary>
        private readonly string spanName;

        /// <summary>
        /// The OpenTelemetry links. These correspond loosely to OpenTracing references.
        /// </summary>
        private readonly List<Link> links = new List<Link>();

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
        private TelemetrySpan parentSpan;

        /// <summary>
        /// The parent as an SpanContext, if any.
        /// </summary>
        private SpanContext parentSpanContext;

        /// <summary>
        /// The explicit start time, if any.
        /// </summary>
        private DateTimeOffset? explicitStartTime;

        private bool ignoreActiveSpan;

        private SpanKind spanKind;

        private bool error;

        public SpanBuilderShim(Tracer tracer, string spanName, IList<string> rootOperationNamesForActivityBasedAutoInstrumentations = null)
        {
            Guard.Null(tracer, nameof(tracer));
            Guard.Null(spanName, nameof(spanName));

            this.tracer = tracer;
            this.spanName = spanName;
            this.ScopeManager = new ScopeManagerShim(this.tracer);
            this.rootOperationNamesForActivityBasedAutoInstrumentations = rootOperationNamesForActivityBasedAutoInstrumentations ?? this.rootOperationNamesForActivityBasedAutoInstrumentations;
        }

        private IScopeManager ScopeManager { get; }

        private bool ParentSet => this.parentSpan != null || this.parentSpanContext.IsValid;

        /// <inheritdoc/>
        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            if (parent == null)
            {
                return this;
            }

            return this.AddReference(References.ChildOf, parent);
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
                this.parentSpan = GetOpenTelemetrySpan(parent);
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

            Guard.Null(referenceType, nameof(referenceType));

            // TODO There is no relation between OpenTracing.References (referenceType) and OpenTelemetry Link
            var actualContext = GetOpenTelemetrySpanContext(referencedContext);
            if (!this.ParentSet)
            {
                this.parentSpanContext = actualContext;
                return this;
            }
            else
            {
                this.links.Add(new Link(actualContext));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder IgnoreActiveSpan()
        {
            this.ignoreActiveSpan = true;
            return this;
        }

        /// <inheritdoc/>
        public ISpan Start()
        {
            TelemetrySpan span = null;

            // If specified, this takes precedence.
            if (this.ignoreActiveSpan)
            {
                span = this.tracer.StartRootSpan(this.spanName, this.spanKind, default, this.links, this.explicitStartTime ?? default);
            }
            else if (this.parentSpan != null)
            {
                span = this.tracer.StartSpan(this.spanName, this.spanKind, this.parentSpan, default, this.links, this.explicitStartTime ?? default);
            }
            else if (this.parentSpanContext.IsValid)
            {
                span = this.tracer.StartSpan(this.spanName, this.spanKind, this.parentSpanContext, default, this.links, this.explicitStartTime ?? default);
            }
            else if (this.parentSpan == null && !this.parentSpanContext.IsValid && Activity.Current != null && Activity.Current.IdFormat == ActivityIdFormat.W3C)
            {
                if (this.rootOperationNamesForActivityBasedAutoInstrumentations.Contains(Activity.Current.OperationName))
                {
                    span = Tracer.CurrentSpan;
                }
            }

            if (span == null)
            {
                span = this.tracer.StartSpan(this.spanName, this.spanKind, default(SpanContext), default, null, this.explicitStartTime ?? default);
            }

            foreach (var kvp in this.attributes)
            {
                span.SetAttribute(kvp.Key, kvp.Value.ToString());
            }

            if (this.error)
            {
                span.SetStatus(Status.Error);
            }

            return new SpanShim(span);
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
            if (global::OpenTracing.Tag.Tags.SpanKind.Key.Equals(key, StringComparison.Ordinal))
            {
                this.spanKind = value switch
                {
                    global::OpenTracing.Tag.Tags.SpanKindClient => SpanKind.Client,
                    global::OpenTracing.Tag.Tags.SpanKindServer => SpanKind.Server,
                    global::OpenTracing.Tag.Tags.SpanKindProducer => SpanKind.Producer,
                    global::OpenTracing.Tag.Tags.SpanKindConsumer => SpanKind.Consumer,
                    _ => SpanKind.Internal,
                };
            }
            else if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key, StringComparison.Ordinal) && bool.TryParse(value, out var booleanValue))
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
            if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key, StringComparison.Ordinal))
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
            Guard.Null(tag?.Key, $"{nameof(tag)}?.{nameof(tag.Key)}");

            return this.WithTag(tag.Key, value);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.IntOrStringTag tag, string value)
        {
            Guard.Null(tag?.Key, $"{nameof(tag)}?.{nameof(tag.Key)}");

            if (int.TryParse(value, out var result))
            {
                return this.WithTag(tag.Key, result);
            }

            return this.WithTag(tag.Key, value);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.IntTag tag, int value)
        {
            Guard.Null(tag?.Key, $"{nameof(tag)}?.{nameof(tag.Key)}");

            return this.WithTag(tag.Key, value);
        }

        /// <inheritdoc/>
        public ISpanBuilder WithTag(global::OpenTracing.Tag.StringTag tag, string value)
        {
            Guard.Null(tag?.Key, $"{nameof(tag)}?.{nameof(tag.Key)}");

            return this.WithTag(tag.Key, value);
        }

        /// <summary>
        /// Gets an implementation of OpenTelemetry TelemetrySpan from the OpenTracing ISpan.
        /// </summary>
        /// <param name="span">The span.</param>
        /// <returns>an implementation of OpenTelemetry TelemetrySpan.</returns>
        /// <exception cref="ArgumentException">span is not a valid SpanShim object.</exception>
        private static TelemetrySpan GetOpenTelemetrySpan(ISpan span)
        {
            var shim = Guard.Type<SpanShim>(span, nameof(span));

            return shim.Span;
        }

        /// <summary>
        /// Gets the OpenTelemetry SpanContext.
        /// </summary>
        /// <param name="spanContext">The span context.</param>
        /// <returns>the OpenTelemetry SpanContext.</returns>
        /// <exception cref="ArgumentException">context is not a valid SpanContextShim object.</exception>
        private static SpanContext GetOpenTelemetrySpanContext(ISpanContext spanContext)
        {
            var shim = Guard.Type<SpanContextShim>(spanContext, nameof(spanContext));

            return shim.SpanContext;
        }
    }
}
