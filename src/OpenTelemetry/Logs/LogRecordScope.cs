// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores details about a scope attached to a log message.
/// </summary>
public sealed class LogRecordScope : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly object? scope;
    private IEnumerable<KeyValuePair<string, object?>> attributes;

    internal LogRecordScope(object? scope)
    {
        this.scope = scope;
        this.attributes = ResolveAttributes(scope);
    }

    /// <summary>
    /// Gets the raw scope value.
    /// </summary>
    public object? Scope => this.scope;

    /// <summary>
    /// Gets or sets the attributes attached to the scope.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object?>> Attributes
    {
        get => this.attributes;
        set
        {
            if (ReferenceEquals(this.attributes, value))
            {
                return;
            }

            this.attributes = value;
        }
    }

    /// <summary>
    /// Gets an <see cref="IEnumerator"/> for looping over the inner values
    /// of the scope.
    /// </summary>
    /// <returns><see cref="IEnumerator"/>.</returns>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => this.attributes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private static IEnumerable<KeyValuePair<string, object?>> ResolveAttributes(object? scope) =>
        scope switch
        {
            IReadOnlyList<KeyValuePair<string, object?>> scopeList => scopeList,
            IEnumerable<KeyValuePair<string, object?>> scopeEnumerable => scopeEnumerable,
            _ => [new KeyValuePair<string, object?>(string.Empty, scope)],
        };
}
