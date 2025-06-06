// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Span represents a single operation within a trace.
/// </summary>
/// <remarks>TelemetrySpan is a wrapper around <see cref="System.Diagnostics.Activity"/> class.</remarks>
public class TelemetrySpan : IDisposable
{
    internal static readonly TelemetrySpan NoopInstance = new(null);
    internal readonly Activity? Activity;

    internal TelemetrySpan(Activity? activity)
    {
        this.Activity = activity;
    }

    /// <summary>
    /// Gets the span context.
    /// </summary>
    public SpanContext Context
        => this.Activity == null ? default : new SpanContext(this.Activity.Context);

    /// <summary>
    /// Gets a value indicating whether this span will be recorded.
    /// </summary>
    public bool IsRecording
        => this.Activity?.IsAllDataRequested == true;

    /// <summary>
    /// Gets the identity of the parent span id, if any.
    /// </summary>
    public ActivitySpanId ParentSpanId
        => this.Activity?.ParentSpanId ?? default;

    /// <summary>
    /// Sets the status of the span execution.
    /// </summary>
    /// <param name="value">Status to be set.</param>
    public void SetStatus(Status value)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        this.Activity.SetStatus(value);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Updates the <see cref="TelemetrySpan"/> name.
    ///
    /// If used, this will override the name provided via StartSpan method overload.
    /// Upon this update, any sampling behavior based on <see cref="TelemetrySpan"/> name will depend on the
    /// implementation.
    /// </summary>
    /// <param name="name">Name of the span.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan UpdateName(string name)
    {
        if (this.Activity != null)
        {
            this.Activity.DisplayName = name;
        }

        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, string? value)
    {
        this.SetAttributeInternal(key, value);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, int value)
    {
        this.SetAttributeInternal(key, value);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, bool value)
    {
        this.SetAttributeInternal(key, value);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, double value)
    {
        this.SetAttributeInternal(key, value);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="values">Attribute values.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, string?[]? values)
    {
        this.SetAttributeInternal(key, values);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="values">Attribute values.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, int[]? values)
    {
        this.SetAttributeInternal(key, values);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="values">Attribute values.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, bool[]? values)
    {
        this.SetAttributeInternal(key, values);
        return this;
    }

    /// <summary>
    /// Sets a new attribute on the span.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="values">Attribute values.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan SetAttribute(string key, double[]? values)
    {
        this.SetAttributeInternal(key, values);
        return this;
    }

    /// <summary>
    /// Adds a single Event to the <see cref="TelemetrySpan"/>.
    /// </summary>
    /// <param name="name">Name of the event.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan AddEvent(string name)
    {
        this.AddEventInternal(name);
        return this;
    }

    /// <summary>
    /// Adds a single Event to the <see cref="TelemetrySpan"/>.
    /// </summary>
    /// <param name="name">Name of the event.</param>
    /// <param name="timestamp">Timestamp of the event.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan AddEvent(string name, DateTimeOffset timestamp)
    {
        this.AddEventInternal(name, timestamp);
        return this;
    }

    /// <summary>
    /// Adds a single Event to the <see cref="TelemetrySpan"/>.
    /// </summary>
    /// <param name="name">Name of the event.</param>
    /// <param name="attributes">Attributes for the event.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan AddEvent(string name, SpanAttributes attributes)
    {
        this.AddEventInternal(name, default, attributes?.Attributes);
        return this;
    }

    /// <summary>
    /// Adds a single Event to the <see cref="TelemetrySpan"/>.
    /// </summary>
    /// <param name="name">Name of the event.</param>
    /// <param name="timestamp">Timestamp of the event.</param>
    /// <param name="attributes">Attributes for the event.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan AddEvent(string name, DateTimeOffset timestamp, SpanAttributes? attributes)
    {
        this.AddEventInternal(name, timestamp, attributes?.Attributes);
        return this;
    }

    /// <summary>
    /// Adds a link to another span.
    /// </summary>
    /// <param name="spanContext">Span context to be linked.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan AddLink(SpanContext spanContext)
    {
        this.AddLinkInternal(spanContext.ActivityContext);
        return this;
    }

    /// <summary>
    /// Adds a link to another span.
    /// </summary>
    /// <param name="spanContext">Span context to be linked.</param>
    /// <param name="attributes">Attributes for the link.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan AddLink(SpanContext spanContext, SpanAttributes? attributes)
    {
        this.AddLinkInternal(spanContext.ActivityContext, attributes?.Attributes);
        return this;
    }

    /// <summary>
    /// End the span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End()
    {
        this.Activity?.Stop();
    }

    /// <summary>
    /// End the span.
    /// </summary>
    /// <param name="endTimestamp">End timestamp.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End(DateTimeOffset endTimestamp)
    {
        var activity = this.Activity;
        if (activity != null)
        {
            activity.SetEndTime(endTimestamp.UtcDateTime);
            activity.Stop();
        }
    }

    /// <summary>
    /// Record Exception.
    /// </summary>
    /// <param name="ex">Exception to be recorded.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan RecordException(Exception? ex)
    {
        if (ex == null)
        {
            return this;
        }

        return this.RecordException(ex.GetType().Name, ex.Message, ex.ToInvariantString());
    }

    /// <summary>
    /// Record Exception.
    /// </summary>
    /// <param name="type">Type of the exception to be recorded.</param>
    /// <param name="message">Message of the exception to be recorded.</param>
    /// <param name="stacktrace">Stacktrace of the exception to be recorded.</param>
    /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
    public TelemetrySpan RecordException(string? type, string? message, string? stacktrace)
    {
        SpanAttributes attributes = new SpanAttributes();

        if (!string.IsNullOrWhiteSpace(type))
        {
            attributes.Add(SemanticConventions.AttributeExceptionType, type);
        }

        if (!string.IsNullOrWhiteSpace(stacktrace))
        {
            attributes.Add(SemanticConventions.AttributeExceptionStacktrace, stacktrace);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            attributes.Add(SemanticConventions.AttributeExceptionMessage, message);
        }

        if (attributes.Attributes.Count != 0)
        {
            this.AddEvent(SemanticConventions.AttributeExceptionEventName, attributes);
        }

        return this;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Marks the span as current.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Activate()
        => Activity.Current = this.Activity;

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Activity?.Dispose();
        }
    }

    private void SetAttributeInternal(string key, object? value)
    {
        if (this.IsRecording)
        {
            this.Activity!.SetTag(key, value);
        }
    }

    private void AddEventInternal(string name, DateTimeOffset timestamp = default, ActivityTagsCollection? tags = null)
    {
        if (this.IsRecording)
        {
            this.Activity!.AddEvent(new ActivityEvent(name, timestamp, tags));
        }
    }

    private void AddLinkInternal(ActivityContext context, ActivityTagsCollection? tags = null)
    {
        if (this.IsRecording)
        {
            this.Activity!.AddLink(new ActivityLink(context, tags));
        }
    }
}
