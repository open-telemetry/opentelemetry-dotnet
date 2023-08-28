// <copyright file="DirectorySizeTracker.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

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
    /// <param name="currentSizeInBytes">Size of blob to be written.</param>
    /// <returns>True if space is available else false.</returns>
    public bool IsSpaceAvailable(out long currentSizeInBytes)
    {
        currentSizeInBytes = Interlocked.Read(ref this.directoryCurrentSizeInBytes);
        return currentSizeInBytes < this.maxSizeInBytes;
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
            foreach (string file in Directory.EnumerateFiles(path))
            {
                if (File.Exists(file))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    directorySize += fileInfo.Length;
                }
            }

            foreach (string dir in Directory.GetDirectories(path))
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
