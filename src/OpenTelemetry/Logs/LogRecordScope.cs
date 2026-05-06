// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores details about a scope attached to a log message.
/// </summary>
public readonly struct LogRecordScope : IEquatable<LogRecordScope>
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
    /// Compare two <see cref="LogRecordScope"/> for equality.
    /// </summary>
    public static bool operator ==(LogRecordScope left, LogRecordScope right) => left.Equals(right);

    /// <summary>
    /// Compare two <see cref="LogRecordScope"/> for inequality.
    /// </summary>
    public static bool operator !=(LogRecordScope left, LogRecordScope right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LogRecordScope other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(LogRecordScope other) => object.Equals(this.Scope, other.Scope);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Scope?.GetHashCode() ?? 0;

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
                this.scope = [.. scopeEnumerable];
            }
            else
            {
                this.scope = [new KeyValuePair<string, object?>(string.Empty, scope)];
            }

            this.position = 0;
            this.Current = default;
        }

        /// <inheritdoc/>
        public KeyValuePair<string, object?> Current { get; private set; }

        readonly object IEnumerator.Current => this.Current;

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
        public readonly void Dispose()
        {
        }

        /// <inheritdoc/>
        public void Reset()
            => throw new NotSupportedException();
    }
}
