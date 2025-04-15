// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Context;

/// <summary>
/// The abstract context slot.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public abstract class RuntimeContextSlot<T> : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeContextSlot{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the context slot.</param>
    protected RuntimeContextSlot(string name)
    {
        Guard.ThrowIfNullOrEmpty(name);

        this.Name = name;
    }

    /// <summary>
    /// Gets the name of the context slot.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Get the value from the context slot.
    /// </summary>
    /// <returns>The value retrieved from the context slot.</returns>
#pragma warning disable CA1716 // Identifiers should not match keywords
    public abstract T? Get();
#pragma warning restore CA1716 // Identifiers should not match keywords

    /// <summary>
    /// Set the value to the context slot.
    /// </summary>
    /// <param name="value">The value to be set.</param>
#pragma warning disable CA1716 // Identifiers should not match keywords
    public abstract void Set(T value);
#pragma warning restore CA1716 // Identifiers should not match keywords

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
