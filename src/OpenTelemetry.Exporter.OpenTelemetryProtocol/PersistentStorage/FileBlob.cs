// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
using OpenTelemetry.PersistentStorage.Abstractions;

namespace OpenTelemetry.PersistentStorage.FileSystem;

/// <summary>
/// The <see cref="FileBlob"/> allows to save a blob
/// in file storage.
/// </summary>

#if BUILDING_INTERNAL_PERSISTENT_STORAGE
internal sealed class FileBlob : PersistentBlob
#else
public class FileBlob : PersistentBlob
#endif
{
    private readonly DirectorySizeTracker? directorySizeTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBlob"/>
    /// class.
    /// </summary>
    /// <param name="fullPath">Absolute file path of the blob.</param>
    public FileBlob(string fullPath)
        : this(fullPath, null)
    {
    }

    internal FileBlob(string fullPath, DirectorySizeTracker? directorySizeTracker)
    {
        this.FullPath = fullPath;
        this.directorySizeTracker = directorySizeTracker;
    }

    public string FullPath { get; private set; }

    protected override bool OnTryRead([NotNullWhen(true)] out byte[]? buffer)
    {
        try
        {
            buffer = File.ReadAllBytes(this.FullPath);
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotReadFileBlob(this.FullPath, ex);
            buffer = null;
            return false;
        }

        return true;
    }

    protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0)
    {
        Guard.ThrowIfNull(buffer);

        return this.OnTryWriteSpan(buffer.AsSpan(), leasePeriodMilliseconds);
    }

    protected override bool OnTryWriteSpan(ReadOnlySpan<byte> buffer, int leasePeriodMilliseconds)
    {
        // If the blob has already been written, its content is replaced. Any leases are removed from its name
        // first, so that leases are never nested and the existing blob is not left behind alongside the new one.
        var released = PersistentStorageHelper.RemoveLeases(this.FullPath);
        var destination = released;

        // The temporary file is given a new name with the current time, rather than being named after the blob, as the
        // storage maintenance times temporary files out using the timestamp in their name. A temporary file named after
        // a blob that was created, or whose lease expired, before the write timeout could otherwise be removed while it
        // is still being written to, such as when an existing blob is written to again.
        var path = PersistentStorageHelper.GetUniqueFilePath(this.FullPath, ".blob.tmp");
        long replacedSize;

        try
        {
            PersistentStorageHelper.WriteAllBytes(path, buffer);

            if (leasePeriodMilliseconds > 0)
            {
                var timestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
                destination += $"@{PersistentStorageHelper.FormatTimestamp(timestamp)}.lock";
            }

            try
            {
                replacedSize = this.MoveIntoPlace(path, destination);
            }
            catch (IOException)
            {
                // The lease may have expired and been released by the storage maintenance while the blob was
                // being moved into place, so try again now that the blob's existing file has been moved.
                replacedSize = this.MoveIntoPlace(path, destination);
            }
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotWriteFileBlob(path, ex);
            return false;
        }

        this.FullPath = destination;
        this.directorySizeTracker?.FileAdded(buffer.Length);

        if (replacedSize > 0)
        {
            this.directorySizeTracker?.FileRemoved(replacedSize);
        }

        return true;
    }

    protected override bool OnTryLease(int leasePeriodMilliseconds)
    {
        var leaseTimestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
        var path = PersistentStorageHelper.RemoveLeases(this.FullPath) + $"@{PersistentStorageHelper.FormatTimestamp(leaseTimestamp)}.lock";

        try
        {
            File.Move(this.FullPath, path);
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotLeaseFileBlob(this.FullPath, ex);
            return false;
        }

        this.FullPath = path;

        return true;
    }

    protected override bool OnTryDelete()
    {
        try
        {
            // If the lease has expired and the blob has been released by the storage maintenance, it is no longer
            // removed, as it may now be leased by another reader, and the file is claimed before it is removed so
            // that the blob cannot be released between it being found and removed and then be left behind.
            var fileSize = PersistentStorageHelper.RemoveClaimedFile(this.FullPath);
            this.directorySizeTracker?.FileRemoved(fileSize);
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotDeleteFileBlob(this.FullPath, ex);
            return false;
        }

        return true;
    }

    private long MoveIntoPlace(string path, string destination)
    {
        // The blob's existing file, if any, is either at its current path or, if its lease has expired and been
        // released by the storage maintenance since it was leased, at its name with one or more of its leases removed.
        var existing = new FileInfo(this.FullPath);

        if (!existing.Exists)
        {
            foreach (var releasedPath in PersistentStorageHelper.GetReleasedPaths(this.FullPath))
            {
                var released = new FileInfo(releasedPath);

                if (released.Exists)
                {
                    existing = released;
                    break;
                }
            }
        }

        if (!existing.Exists)
        {
            File.Move(path, destination);
            return 0;
        }

        var replacedSize = existing.Length;

        if (string.Equals(existing.FullName, Path.GetFullPath(destination), StringComparison.Ordinal))
        {
            File.Replace(path, destination, destinationBackupFileName: null);
            return replacedSize;
        }

        File.Move(path, destination);

        return PersistentStorageHelper.RemoveReplacedFile(existing.FullName, destination);
    }
}
