// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing;

internal sealed class SpanShim : ISpan
{
    /// <summary>
    /// The default event name if not specified.
    /// </summary>
    public const string DefaultEventName = "log";

    private static readonly IReadOnlyCollection<Type> OpenTelemetrySupportedAttributeValueTypes =
    [
        typeof(string),
        typeof(bool),
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
    ];

    private readonly SpanContextShim spanContextShim;

    public SpanShim(TelemetrySpan span)
    {
        Guard.ThrowIfNull(span);

        this.Span = span;
        this.spanContextShim = new SpanContextShim(this.Span.Context);
    }

    /// <inheritdoc/>
    public ISpanContext Context => this.spanContextShim;

    public TelemetrySpan Span { get; }

    /// <inheritdoc/>
    public void Finish()
    {
        this.Span.End();
    }

    /// <inheritdoc/>
    public void Finish(DateTimeOffset finishTimestamp)
    {
        this.Span.End(finishTimestamp);
    }

    /// <inheritdoc/>
    public string? GetBaggageItem(string key)
        => Baggage.GetBaggage(key);

    /// <inheritdoc/>
    public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
    {
        Guard.ThrowIfNull(fields);

        var payload = ConvertToEventPayload(fields);
        var eventName = payload.Item1;

        var spanAttributes = new SpanAttributes();
        foreach (var field in payload.Item2)
        {
            switch (field.Value)
            {
                case long value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case long[] value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case bool value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case bool[] value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case double value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case double[] value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case string value:
                    spanAttributes.Add(field.Key, value);
                    break;
                case string[] value:
                    spanAttributes.Add(field.Key, value);
                    break;

                default:
                    break;
            }
        }

        if (timestamp == DateTimeOffset.MinValue)
        {
            this.Span.AddEvent(eventName, spanAttributes);
        }
        else
        {
            this.Span.AddEvent(eventName, timestamp, spanAttributes);
        }

        return this;
    }

    /// <inheritdoc/>
    public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
    {
        return this.Log(DateTimeOffset.MinValue, fields);
    }

    /// <inheritdoc/>
    public ISpan Log(string @event)
    {
        Guard.ThrowIfNull(@event);

        this.Span.AddEvent(@event);
        return this;
    }

    /// <inheritdoc/>
    public ISpan Log(DateTimeOffset timestamp, string @event)
    {
        Guard.ThrowIfNull(@event);

        this.Span.AddEvent(@event, timestamp);
        return this;
    }

    /// <inheritdoc/>
    public ISpan SetBaggageItem(string key, string? value)
    {
        Baggage.SetBaggage(key, value);
        return this;
    }

    /// <inheritdoc/>
    public ISpan SetOperationName(string operationName)
    {
        Guard.ThrowIfNull(operationName);

        this.Span.UpdateName(operationName);
        return this;
    }

    /// <inheritdoc/>
    public ISpan SetTag(string key, string? value)
    {
        Guard.ThrowIfNull(key);

        this.Span.SetAttribute(key, value);
        return this;
    }

    /// <inheritdoc/>
    public ISpan SetTag(string key, bool value)
    {
        Guard.ThrowIfNull(key);

        // Special case the OpenTracing Error Tag
        // see https://opentracing.io/specification/conventions/
        if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key, StringComparison.Ordinal))
        {
            this.Span.SetStatus(value ? Status.Error : Status.Ok);
        }
        else
        {
            this.Span.SetAttribute(key, value);
        }

        return this;
    }

    /// <inheritdoc/>
    public ISpan SetTag(string key, int value)
    {
        Guard.ThrowIfNull(key);

        this.Span.SetAttribute(key, value);
        return this;
    }

    /// <inheritdoc/>
    public ISpan SetTag(string key, double value)
    {
        Guard.ThrowIfNull(key);

        this.Span.SetAttribute(key, value);
        return this;
    }

    /// <inheritdoc/>
    public ISpan SetTag(global::OpenTracing.Tag.BooleanTag tag, bool value)
    {
        Guard.ThrowIfNull(tag);

        return this.SetTag(tag.Key, value);
    }

    /// <inheritdoc/>
    public ISpan SetTag(global::OpenTracing.Tag.IntOrStringTag tag, string? value)
    {
        Guard.ThrowIfNull(tag);

        if (value != null && int.TryParse(value, out var result))
        {
            return this.SetTag(tag.Key, result);
        }

        return this.SetTag(tag.Key, value);
    }

    /// <inheritdoc/>
    public ISpan SetTag(global::OpenTracing.Tag.IntTag tag, int value)
    {
        Guard.ThrowIfNull(tag);

        return this.SetTag(tag.Key, value);
    }

    /// <inheritdoc/>
    public ISpan SetTag(global::OpenTracing.Tag.StringTag tag, string? value)
    {
        Guard.ThrowIfNull(tag);

        return this.SetTag(tag.Key, value);
    }

    /// <summary>
    /// Constructs an OpenTelemetry event payload from an OpenTracing Log key/value map.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <returns>A 2-Tuple containing the event name and payload information.</returns>
    private static Tuple<string, IDictionary<string, object>> ConvertToEventPayload(IEnumerable<KeyValuePair<string, object>> fields)
    {
        string? eventName = null;
        var attributes = new Dictionary<string, object>();

        foreach (var field in fields)
        {
            // TODO verify null values are NOT allowed.
            if (field.Value == null)
            {
                continue;
            }

            // Duplicate keys must be ignored even though they appear to be allowed in OpenTracing.
            if (attributes.ContainsKey(field.Key))
            {
                continue;
            }

            if (eventName == null && field.Key.Equals(LogFields.Event, StringComparison.Ordinal) && field.Value is string value)
            {
                // This is meant to be the event name
                eventName = value;

                // We don't want to add the event name as a separate attribute
                continue;
            }

            // Supported types are added directly, all other types are converted to strings.
            if (OpenTelemetrySupportedAttributeValueTypes.Contains(field.Value.GetType()))
            {
                attributes.Add(field.Key, field.Value);
            }
            else
            {
                // TODO should we completely ignore unsupported types?
                attributes.Add(field.Key, field.Value.ToString()!);
            }
        }

        return new Tuple<string, IDictionary<string, object>>(eventName ?? DefaultEventName, attributes);
    }
}
