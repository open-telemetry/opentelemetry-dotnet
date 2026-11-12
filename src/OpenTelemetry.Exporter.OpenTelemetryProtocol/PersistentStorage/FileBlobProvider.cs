// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Timers;
using OpenTelemetry.Internal;
using OpenTelemetry.PersistentStorage.Abstractions;

namespace OpenTelemetry.PersistentStorage.FileSystem;

/// <summary>
/// Persistent file storage <see cref="FileBlobProvider"/> allows to save data
/// as blobs in file storage.
/// </summary>

#if BUILDING_INTERNAL_PERSISTENT_STORAGE
internal sealed class FileBlobProvider : PersistentBlobProvider, IDisposable
#else
public class FileBlobProvider : PersistentBlobProvider, IDisposable
#endif
{
    internal readonly string DirectoryPath;
    private readonly DirectorySizeTracker directorySizeTracker;
    private readonly long retentionPeriodInMilliseconds;
    private readonly int writeTimeoutInMilliseconds;
    private readonly System.Timers.Timer maintenanceTimer;
    private readonly bool checkPermissions;
    private int directoryRejected;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBlobProvider"/>
    /// class.
    /// </summary>
    /// <param name="path">
    /// Sets file storage folder location where blobs are stored.
    /// </param>
    /// <param name="maxSizeInBytes">
    /// Maximum allowed storage folder size.
    /// Default is 50 MB.
    /// </param>
    /// <param name="maintenancePeriodInMilliseconds">
    /// Maintenance event runs at specified interval.
    /// Removes expired leases and blobs that exceed retention period.
    /// Default is 2 minutes.
    /// </param>
    /// <param name="retentionPeriodInMilliseconds">
    /// Retention period in milliseconds for the blob.
    /// Default is 2 days.
    /// </param>
    /// <param name="writeTimeoutInMilliseconds">
    /// Controls the timeout when writing a buffer to blob.
    /// Default is 1 minute.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// path is null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// invalid path.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// path exceeds system defined maximum length.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// insufficient privileges for provided path, or, on .NET on non-Windows platforms, the
    /// path is not owned by the current user or its contents could be changed by another user.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// path contains a colon character (:) that is not part of a drive label ("C:\").
    /// </exception>
    /// <exception cref="ArgumentException">
    /// path contains invalid characters.
    /// </exception>
    /// <exception cref="IOException">
    /// path is either file or network name is not known.
    /// </exception>
    public FileBlobProvider(
        string path,
        long maxSizeInBytes = 52428800,
        int maintenancePeriodInMilliseconds = 120000,
        long retentionPeriodInMilliseconds = 172800000,
        int writeTimeoutInMilliseconds = 60000)
        : this(path, checkPermissions: true, maxSizeInBytes, maintenancePeriodInMilliseconds, retentionPeriodInMilliseconds, writeTimeoutInMilliseconds)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBlobProvider"/>
    /// class, optionally without verifying that the storage directory is private to the current user.
    /// </summary>
    /// <remarks>
    /// Blobs are replayed with the credentials of the application, so on .NET on non-Windows platforms the
    /// storage directory is verified to not be changeable by other users. Only disable the verification
    /// when the permissions of the directory cannot be made to satisfy it, such as on a volume that
    /// does not support them, and the directory is otherwise protected from other users.
    /// The default maximum size, maintenance period, retention period and write timeout are used.
    /// </remarks>
    /// <param name="path">
    /// Sets file storage folder location where blobs are stored.
    /// </param>
    /// <param name="checkPermissions">
    /// Whether to verify, on .NET on non-Windows platforms, that the storage directory is owned by the current user
    /// and cannot be changed by other users, including any directory in its path.
    /// The check is repeated before blobs are listed, created or removed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// path is null.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// insufficient privileges for provided path, or, when <paramref name="checkPermissions"/> is
    /// <see langword="true"/> on .NET on non-Windows platforms, the path is not owned by the current user
    /// or its contents could be changed by another user.
    /// </exception>
    public FileBlobProvider(string path, bool checkPermissions)
        : this(path, checkPermissions, 52428800, 120000, 172800000, 60000)
    {
    }

    internal FileBlobProvider(
        string path,
        bool checkPermissions,
        long maxSizeInBytes,
        int maintenancePeriodInMilliseconds,
        long retentionPeriodInMilliseconds,
        int writeTimeoutInMilliseconds)
    {
        Guard.ThrowIfNull(path);

        // TODO: Validate time period values
        this.checkPermissions = checkPermissions;

        // A relative path is resolved once, so that the directory that is verified is the directory that is always used
        // for blobs and for size tracking, even if the current directory of the process is changed after this point.
        this.DirectoryPath = PersistentStorageHelper.CreateSubdirectory(Path.GetFullPath(path), checkPermissions);
        this.directorySizeTracker = new DirectorySizeTracker(maxSizeInBytes, this.DirectoryPath);
        this.retentionPeriodInMilliseconds = retentionPeriodInMilliseconds;
        this.writeTimeoutInMilliseconds = writeTimeoutInMilliseconds;

        this.maintenanceTimer = new System.Timers.Timer(maintenancePeriodInMilliseconds);
        this.maintenanceTimer.Elapsed += this.OnMaintenanceEvent;
        this.maintenanceTimer.AutoReset = true;
        this.maintenanceTimer.Enabled = true;
    }

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected override IEnumerable<PersistentBlob> OnGetBlobs()
    {
        // Blobs are replayed with the exporter's credentials, so none are read from a directory that is no longer trusted.
        if (!this.IsDirectoryTrusted())
        {
            yield break;
        }

        var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(this.retentionPeriodInMilliseconds);

        foreach (var file in Directory.EnumerateFiles(this.DirectoryPath, "*.blob", SearchOption.TopDirectoryOnly).OrderByDescending(f => f))
        {
            // The storage directory can be shared with files that were not created by this component, which must not be
            // leased, read and deleted as if they were blobs. On Windows, *.blob also matches differently cased extensions.
            if (!PersistentStorageHelper.IsBlobFileName(Path.GetFileName(file)))
            {
                continue;
            }

            var fileDateTime = PersistentStorageHelper.GetBlobCreationTime(file);
            if (fileDateTime > retentionDeadline)
            {
                yield return new FileBlob(file, this.directorySizeTracker, this.IsDirectoryTrusted);
            }
        }
    }

    protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, [NotNullWhen(true)] out PersistentBlob? blob)
    {
        Guard.ThrowIfNull(buffer);

        blob = this.CreateFileBlob(buffer.AsSpan(), leasePeriodMilliseconds);

        return blob != null;
    }

    protected override bool OnTryCreateBlob(byte[] buffer, [NotNullWhen(true)] out PersistentBlob? blob)
    {
        Guard.ThrowIfNull(buffer);

        blob = this.CreateFileBlob(buffer.AsSpan());

        return blob != null;
    }

    protected override bool OnTryCreateBlob(ReadOnlySpan<byte> buffer, int leasePeriodMilliseconds, [NotNullWhen(true)] out PersistentBlob? blob)
    {
        blob = this.CreateFileBlob(buffer, leasePeriodMilliseconds);

        return blob != null;
    }

    protected override bool OnTryCreateBlob(ReadOnlySpan<byte> buffer, [NotNullWhen(true)] out PersistentBlob? blob)
    {
        blob = this.CreateFileBlob(buffer);

        return blob != null;
    }

    protected override bool OnTryGetBlob([NotNullWhen(true)] out PersistentBlob? blob)
    {
        blob = this.OnGetBlobs().FirstOrDefault();

        return blob != null;
    }

#if BUILDING_INTERNAL_PERSISTENT_STORAGE
    private void Dispose(bool disposing)
#else
    protected virtual void Dispose(bool disposing)
#endif
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.maintenanceTimer.Dispose();
            }

            this.disposedValue = true;
        }
    }

    private void OnMaintenanceEvent(object? source, ElapsedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(this.DirectoryPath))
            {
                PersistentStorageHelper.CreateSubdirectory(this.DirectoryPath, this.checkPermissions);
            }
        }
        catch (Exception)
        {
            return;
        }

        // The directory may have been removed and replaced by another user since it was last verified (for example
        // beneath a sticky directory such as /tmp, where only its owner can remove it), so it is verified again
        // even when it exists, and no files are removed from it if it can no longer be trusted.
        if (!this.IsDirectoryTrusted())
        {
            return;
        }

        PersistentStorageHelper.RemoveExpiredBlobs(this.DirectoryPath, this.retentionPeriodInMilliseconds, this.writeTimeoutInMilliseconds);

        // It is faster to calculate the directory size, instead of removing length of expired files.
        this.directorySizeTracker.RecountCurrentSize();
    }

    private bool IsDirectoryTrusted()
    {
        try
        {
            PersistentStorageHelper.VerifyDirectory(this.DirectoryPath, this.checkPermissions);
            Volatile.Write(ref this.directoryRejected, 0);
            return true;
        }
        catch (Exception ex)
        {
            // Fail closed, but only report the first failure so that a directory that stays untrusted does not flood the log.
            if (Interlocked.Exchange(ref this.directoryRejected, 1) == 0)
            {
                PersistentStorageEventSource.Log.PersistentStorageException(
                    nameof(FileBlobProvider),
                    $"The directory {this.DirectoryPath} can no longer be trusted, so it is not used",
                    ex);
            }

            return false;
        }
    }

    private bool CheckStorageSize(int bufferLength)
    {
        if (!this.directorySizeTracker.IsSpaceAvailable(bufferLength, out var size))
        {
            // TODO: check accuracy of size reporting.
            PersistentStorageEventSource.Log.PersistentStorageWarning(
                nameof(FileBlobProvider),
                $"Persistent storage max capacity has been reached. Currently at {size / 1024} KiB. Please consider increasing the value of storage max size in exporter config.");
            return false;
        }

        return true;
    }

    private FileBlob? CreateFileBlob(ReadOnlySpan<byte> buffer, int leasePeriodMilliseconds = 0)
    {
        if (!this.IsDirectoryTrusted() || !this.CheckStorageSize(buffer.Length))
        {
            return null;
        }

        try
        {
            var blobFilePath = Path.Combine(this.DirectoryPath, PersistentStorageHelper.GetUniqueFileName(".blob"));
            var blob = new FileBlob(blobFilePath, this.directorySizeTracker, this.IsDirectoryTrusted);

            return blob.TryWrite(buffer, leasePeriodMilliseconds) ? blob : null;
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotCreateFileBlob(ex);
            return null;
        }
    }
}
