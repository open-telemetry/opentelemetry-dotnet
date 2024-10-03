// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// A class that represents the span attributes. Read more here https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/common/README.md#attribute.
/// </summary>
/// <remarks>SpanAttributes is a wrapper around <see cref="ActivityTagsCollection"/> class.</remarks>
public class SpanAttributes
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpanAttributes"/> class.
    /// </summary>
    public SpanAttributes()
    {
        this.Attributes = new ActivityTagsCollection();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanAttributes"/> class.
    /// </summary>
    /// <param name="attributes">Initial attributes to store in the collection.</param>
    public SpanAttributes(IEnumerable<KeyValuePair<string, object?>> attributes)
        : this()
    {
        Guard.ThrowIfNull(attributes);

        foreach (KeyValuePair<string, object?> kvp in attributes)
        {
            this.AddInternal(kvp.Key, kvp.Value);
        }
    }

    internal ActivityTagsCollection Attributes { get; }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="value">Entry value.</param>
    public void Add(string key, long value)
    {
        this.AddInternal(key, value);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="value">Entry value.</param>
    public void Add(string key, string? value)
    {
        this.AddInternal(key, value);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="value">Entry value.</param>
    public void Add(string key, bool value)
    {
        this.AddInternal(key, value);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="value">Entry value.</param>
    public void Add(string key, double value)
    {
        this.AddInternal(key, value);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="values">Entry value.</param>
    public void Add(string key, long[]? values)
    {
        this.AddInternal(key, values);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="values">Entry value.</param>
    public void Add(string key, string?[]? values)
    {
        this.AddInternal(key, values);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="values">Entry value.</param>
    public void Add(string key, bool[]? values)
    {
        this.AddInternal(key, values);
    }

    /// <summary>
    /// Add entry to the attributes.
    /// </summary>
    /// <param name="key">Entry key.</param>
    /// <param name="values">Entry value.</param>
    public void Add(string key, double[]? values)
    {
        this.AddInternal(key, values);
    }

    private void AddInternal(string key, object? value)
    {
        Guard.ThrowIfNull(key);

        this.Attributes[key] = value;
    }
}
