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
    /// Compare two <see cref="LogRecordScope"/> for equality.
    /// </summary>
    /// <param name="scope1">First scope to compare.</param>
    /// <param name="scope2">Second scope to compare.</param>
    public static bool operator ==(LogRecordScope scope1, LogRecordScope scope2) => scope1.Equals(scope2);

    /// <summary>
    /// Compare two <see cref="LogRecordScope"/> for not equality.
    /// </summary>
    /// <param name="scope1">First scope to compare.</param>
    /// <param name="scope2">Second scope to compare.</param>
    public static bool operator !=(LogRecordScope scope1, LogRecordScope scope2) => !scope1.Equals(scope2);

    /// <summary>
    /// Gets an <see cref="IEnumerator"/> for looping over the inner values
    /// of the scope.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this.Scope);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is LogRecordScope other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.Scope?.GetHashCode() ?? 0;

    /// <inheritdoc/>
    public bool Equals(LogRecordScope other)
        => Equals(this.Scope, other.Scope);

    /// <summary>
    /// LogRecordScope enumerator.
    /// </summary>
    // Note: Does not implement equality - enumerators are mutable cursors
    // and comparing instances is not a supported scenario.
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        private readonly IReadOnlyList<KeyValuePair<string, object?>>? scope;
        private readonly IEnumerator<KeyValuePair<string, object?>>? enumerator;
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
                this.enumerator = null;
            }
            else if (scope is IEnumerable<KeyValuePair<string, object?>> scopeEnumerable)
            {
                this.scope = null;
                this.enumerator = scopeEnumerable.GetEnumerator();
            }
            else
            {
                this.scope = [new KeyValuePair<string, object?>(string.Empty, scope)];
                this.enumerator = null;
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
            if (this.enumerator != null)
            {
                if (this.enumerator.MoveNext())
                {
                    this.Current = this.enumerator.Current;
                    return true;
                }

                return false;
            }

            if (this.scope != null && this.position < this.scope.Count)
            {
                this.Current = this.scope[this.position++];
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public readonly void Dispose()
            => this.enumerator?.Dispose();

        /// <inheritdoc/>
        public void Reset()
            => throw new NotSupportedException();
    }
}
