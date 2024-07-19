// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.PersistentStorage.Abstractions;

/// <summary>
/// Represents a persistent blob.
/// </summary>
#if BUILDING_INTERNAL_PERSISTENT_STORAGE
internal abstract class PersistentBlob
#else
public abstract class PersistentBlob
#endif
{
    /// <summary>
    /// Attempts to read the content from the blob.
    /// </summary>
    /// <param name="buffer">
    /// The content to be read.
    /// </param>
    /// <returns>
    /// True if read was successful or else false.
    /// </returns>
    public bool TryRead([NotNullWhen(true)] out byte[]? buffer)
    {
        try
        {
            return this.OnTryRead(out buffer);
        }
        catch (Exception ex)
        {
            PersistentStorageAbstractionsEventSource.Log.PersistentStorageAbstractionsException(nameof(PersistentBlob), "Failed to read the blob.", ex);
            buffer = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to write the given content to the blob.
    /// </summary>
    /// <param name="buffer">
    /// The content to be written.
    /// </param>
    /// <param name="leasePeriodMilliseconds">
    /// The number of milliseconds to lease after the write operation finished.
    /// </param>
    /// <returns>
    /// True if the write operation succeeded or else false.
    /// </returns>
    public bool TryWrite(byte[] buffer, int leasePeriodMilliseconds = 0)
    {
        try
        {
            return this.OnTryWrite(buffer, leasePeriodMilliseconds);
        }
        catch (Exception ex)
        {
            PersistentStorageAbstractionsEventSource.Log.PersistentStorageAbstractionsException(nameof(PersistentBlob), "Failed to write the blob", ex);
            return false;
        }
    }

    /// <summary>
    /// Attempts to acquire lease on the blob.
    /// </summary>
    /// <param name="leasePeriodMilliseconds">
    /// The number of milliseconds to lease.
    /// </param>
    /// <returns>
    /// true if lease is acquired or else false.
    /// </returns>
    public bool TryLease(int leasePeriodMilliseconds)
    {
        try
        {
            return this.OnTryLease(leasePeriodMilliseconds);
        }
        catch (Exception ex)
        {
            PersistentStorageAbstractionsEventSource.Log.PersistentStorageAbstractionsException(nameof(PersistentBlob), "Failed to lease the blob", ex);
            return false;
        }
    }

    /// <summary>
    /// Attempts to delete the blob.
    /// </summary>
    /// <returns>
    /// True if delete was successful else false.
    /// </returns>
    public bool TryDelete()
    {
        try
        {
            return this.OnTryDelete();
        }
        catch (Exception ex)
        {
            PersistentStorageAbstractionsEventSource.Log.PersistentStorageAbstractionsException(nameof(PersistentBlob), "Failed to delete the blob", ex);
            return false;
        }
    }

    protected abstract bool OnTryRead([NotNullWhen(true)] out byte[]? buffer);

    protected abstract bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0);

    protected abstract bool OnTryLease(int leasePeriodMilliseconds);

    protected abstract bool OnTryDelete();
}
