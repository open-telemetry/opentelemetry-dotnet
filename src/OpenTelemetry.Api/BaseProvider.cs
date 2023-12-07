// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry;

/// <summary>
/// Contains logic shared by all OpenTelemetry providers.
/// </summary>
public abstract class BaseProvider : IDisposable
{
    /// <summary>
    /// Finalizes an instance of the <see cref="BaseProvider"/> class.
    /// </summary>
    ~BaseProvider()
    {
        this.Dispose(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
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