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
    /// insufficient privileges for provided path.
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
    {
        Guard.ThrowIfNull(path);

        // TODO: Validate time period values
        this.DirectoryPath = PersistentStorageHelper.CreateSubdirectory(path);
        this.directorySizeTracker = new DirectorySizeTracker(maxSizeInBytes, path);
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
        var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(this.retentionPeriodInMilliseconds);

        foreach (var file in Directory.EnumerateFiles(this.DirectoryPath, "*.blob", SearchOption.TopDirectoryOnly).OrderByDescending(f => f))
        {
            var fileDateTime = PersistentStorageHelper.GetDateTimeFromBlobName(file);
            if (fileDateTime > retentionDeadline)
            {
                yield return new FileBlob(file, this.directorySizeTracker);
            }
        }
    }

    protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, [NotNullWhen(true)] out PersistentBlob? blob)
    {
        blob = this.CreateFileBlob(buffer, leasePeriodMilliseconds);

        return blob != null;
    }

    protected override bool OnTryCreateBlob(byte[] buffer, [NotNullWhen(true)] out PersistentBlob? blob)
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
                Directory.CreateDirectory(this.DirectoryPath);
            }
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.PersistentStorageException(nameof(FileBlobProvider), $"Error creating directory {this.DirectoryPath}", ex);
            return;
        }

        PersistentStorageHelper.RemoveExpiredBlobs(this.DirectoryPath, this.retentionPeriodInMilliseconds, this.writeTimeoutInMilliseconds);

        // It is faster to calculate the directory size, instead of removing length of expired files.
        this.directorySizeTracker.RecountCurrentSize();
    }

    private bool CheckStorageSize()
    {
        if (!this.directorySizeTracker.IsSpaceAvailable(out var size))
        {
            // TODO: check accuracy of size reporting.
            PersistentStorageEventSource.Log.PersistentStorageWarning(
                nameof(FileBlobProvider),
                $"Persistent storage max capacity has been reached. Currently at {size / 1024} KiB. Please consider increasing the value of storage max size in exporter config.");
            return false;
        }

        return true;
    }

    private FileBlob? CreateFileBlob(byte[] buffer, int leasePeriodMilliseconds = 0)
    {
        if (!this.CheckStorageSize())
        {
            return null;
        }

        try
        {
            var blobFilePath = Path.Combine(this.DirectoryPath, PersistentStorageHelper.GetUniqueFileName(".blob"));
            var blob = new FileBlob(blobFilePath, this.directorySizeTracker);

            return blob.TryWrite(buffer, leasePeriodMilliseconds) ? blob : null;
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotCreateFileBlob(ex);
            return null;
        }
    }
}
