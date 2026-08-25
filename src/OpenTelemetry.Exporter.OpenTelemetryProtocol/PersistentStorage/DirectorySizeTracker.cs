// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.PersistentStorage.FileSystem;

/// <summary>
/// Tracks the available storage in a specified directory.
/// </summary>
internal sealed class DirectorySizeTracker
{
    private readonly long maxSizeInBytes;
    private readonly string path;
    private long directoryCurrentSizeInBytes;

    public DirectorySizeTracker(long maxSizeInBytes, string path)
    {
        this.maxSizeInBytes = maxSizeInBytes;
        this.path = path;
        this.directoryCurrentSizeInBytes = CalculateFolderSize(path);
    }

    public void FileAdded(long fileSizeInBytes) => Interlocked.Add(ref this.directoryCurrentSizeInBytes, fileSizeInBytes);

    public void FileRemoved(long fileSizeInBytes) => Interlocked.Add(ref this.directoryCurrentSizeInBytes, fileSizeInBytes * -1);

    /// <summary>
    /// Checks if the space is available for new blob.
    /// </summary>
    /// <remarks>
    /// This method is not thread safe and may give false positives/negatives.
    /// False positive is ok because the file write will eventually fail.
    /// False negative is ok as the file write can be retried if needed.
    /// This is done in order to avoid acquiring lock while writing/deleting the blobs.
    /// </remarks>
    /// <param name="additionalSizeInBytes">Size of blob to be written.</param>
    /// <param name="currentSizeInBytes">Current tracked directory size.</param>
    /// <returns>True if space is available else false.</returns>
    public bool IsSpaceAvailable(long additionalSizeInBytes, out long currentSizeInBytes)
    {
        currentSizeInBytes = Interlocked.Read(ref this.directoryCurrentSizeInBytes);
        return additionalSizeInBytes >= 0
            && currentSizeInBytes >= 0
            && currentSizeInBytes <= this.maxSizeInBytes
            && additionalSizeInBytes <= this.maxSizeInBytes - currentSizeInBytes;
    }

    public void RecountCurrentSize()
    {
        var size = CalculateFolderSize(this.path);
        Interlocked.Exchange(ref this.directoryCurrentSizeInBytes, size);
    }

    internal static long CalculateFolderSize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        long directorySize = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (File.Exists(file))
                {
                    var fileInfo = new FileInfo(file);
                    directorySize += fileInfo.Length;
                }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                directorySize += CalculateFolderSize(dir);
            }
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.PersistentStorageException(nameof(PersistentStorageHelper), "Error calculating folder size", ex);
        }

        return directorySize;
    }
}
