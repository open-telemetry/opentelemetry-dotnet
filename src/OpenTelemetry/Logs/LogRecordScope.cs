// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores details about a scope attached to a log message.
/// </summary>
public readonly struct LogRecordScope
{
    internal LogRecordScope(object? scope)
    {
        this.Scope = scope;
    }

    /// <summary>
    /// Gets the raw scope value.
    /// </summary>
    public object? Scope { get; }

    /// <summary>
    /// Gets an <see cref="IEnumerator"/> for looping over the inner values
    /// of the scope.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this.Scope);

    /// <summary>
    /// LogRecordScope enumerator.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>
    {
        private readonly IReadOnlyList<KeyValuePair<string, object?>> scope;
        private int position;

        /// <summary>
        /// Initializes a new instance of the <see cref="Enumerator"/> struct.
        /// </summary>
        /// <param name="scope">Scope.</param>
        public Enumerator(object? scope)
        {
            if (scope is IReadOnlyList<KeyValuePair<string, object?>> scopeList)
            {
                this.scope = scopeList;
            }
            else if (scope is IEnumerable<KeyValuePair<string, object?>> scopeEnumerable)
            {
                this.scope = new List<KeyValuePair<string, object?>>(scopeEnumerable);
            }
            else
            {
                this.scope = new List<KeyValuePair<string, object?>>
                {
                    new KeyValuePair<string, object?>(string.Empty, scope),
                };
            }

            this.position = 0;
            this.Current = default;
        }

        /// <inheritdoc/>
        public KeyValuePair<string, object?> Current { get; private set; }

        object IEnumerator.Current => this.Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (this.position < this.scope.Count)
            {
                this.Current = this.scope[this.position++];
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public void Reset()
            => throw new NotSupportedException();
    }
}