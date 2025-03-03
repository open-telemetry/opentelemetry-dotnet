// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing;

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
    private readonly List<Link> links = [];

    /// <summary>
    /// The OpenTelemetry attributes. These correspond to OpenTracing Tags.
    /// </summary>
    private readonly SpanAttributes attributes = new();

    /// <summary>
    /// The parent as an TelemetrySpan, if any.
    /// </summary>
    private TelemetrySpan? parentSpan;

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

    public SpanBuilderShim(Tracer tracer, string spanName)
    {
        Guard.ThrowIfNull(tracer);
        Guard.ThrowIfNull(spanName);

        this.tracer = tracer;
        this.spanName = spanName;
        this.ScopeManager = new ScopeManagerShim();
    }

    private ScopeManagerShim ScopeManager { get; }

    private bool ParentSet => this.parentSpan != null || this.parentSpanContext.IsValid;

    /// <inheritdoc/>
    public ISpanBuilder AsChildOf(ISpanContext? parent)
    {
        if (parent == null)
        {
            return this;
        }

        return this.AddReference(References.ChildOf, parent);
    }

    /// <inheritdoc/>
    public ISpanBuilder AsChildOf(ISpan? parent)
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
    public ISpanBuilder AddReference(string referenceType, ISpanContext? referencedContext)
    {
        if (referencedContext == null)
        {
            return this;
        }

        Guard.ThrowIfNull(referenceType);

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
        TelemetrySpan? span = null;

        // If specified, this takes precedence.
        if (this.ignoreActiveSpan)
        {
            span = this.tracer.StartRootSpan(this.spanName, this.spanKind, this.attributes, this.links, this.explicitStartTime ?? default);
        }
        else if (this.parentSpan != null)
        {
            span = this.tracer.StartSpan(this.spanName, this.spanKind, this.parentSpan, this.attributes, this.links, this.explicitStartTime ?? default);
        }
        else if (this.parentSpanContext.IsValid)
        {
            span = this.tracer.StartSpan(this.spanName, this.spanKind, this.parentSpanContext, this.attributes, this.links, this.explicitStartTime ?? default);
        }

        if (span == null)
        {
            span = this.tracer.StartSpan(this.spanName, this.spanKind, default(SpanContext), this.attributes, null, this.explicitStartTime ?? default);
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
    public ISpanBuilder WithTag(string key, string? value)
    {
        if (key == null)
        {
            return this;
        }

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
            this.attributes.Add(key, value);
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
            this.attributes.Add(key, value);
        }

        return this;
    }

    /// <inheritdoc/>
    public ISpanBuilder WithTag(string key, int value)
    {
        this.attributes.Add(key, value);
        return this;
    }

    /// <inheritdoc/>
    public ISpanBuilder WithTag(string key, double value)
    {
        this.attributes.Add(key, value);
        return this;
    }

    /// <inheritdoc/>
    public ISpanBuilder WithTag(global::OpenTracing.Tag.BooleanTag tag, bool value)
    {
        Guard.ThrowIfNull(tag);

        return this.WithTag(tag.Key, value);
    }

    /// <inheritdoc/>
    public ISpanBuilder WithTag(global::OpenTracing.Tag.IntOrStringTag tag, string? value)
    {
        Guard.ThrowIfNull(tag);

        if (value != null && int.TryParse(value, out var result))
        {
            return this.WithTag(tag.Key, result);
        }

        return this.WithTag(tag.Key, value);
    }

    /// <inheritdoc/>
    public ISpanBuilder WithTag(global::OpenTracing.Tag.IntTag tag, int value)
    {
        Guard.ThrowIfNull(tag);

        return this.WithTag(tag.Key, value);
    }

    /// <inheritdoc/>
    public ISpanBuilder WithTag(global::OpenTracing.Tag.StringTag tag, string? value)
    {
        Guard.ThrowIfNull(tag);

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
        var shim = Guard.ThrowIfNotOfType<SpanShim>(span);

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
        var shim = Guard.ThrowIfNotOfType<SpanContextShim>(spanContext);

        return shim.SpanContext;
    }
}
