// <copyright file="FileBlob.cs" company="OpenTelemetry Authors">
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

        string path = this.FullPath + ".tmp";

        try
        {
            PersistentStorageHelper.WriteAllBytes(path, buffer);

            if (leasePeriodMilliseconds > 0)
            {
                var timestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
                this.FullPath += $"@{timestamp:yyyy-MM-ddTHHmmss.fffffffZ}.lock";
            }

            File.Move(path, this.FullPath);
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotWriteFileBlob(path, ex);
            return false;
        }

        this.directorySizeTracker?.FileAdded(buffer.LongLength);
        return true;
    }

    protected override bool OnTryLease(int leasePeriodMilliseconds)
    {
        var path = this.FullPath;
        var leaseTimestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
        if (path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(0, path.LastIndexOf('@'));
        }

        path += $"@{leaseTimestamp:yyyy-MM-ddTHHmmss.fffffffZ}.lock";

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
            PersistentStorageHelper.RemoveFile(this.FullPath, out var fileSize);
            this.directorySizeTracker?.FileRemoved(fileSize);
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotDeleteFileBlob(this.FullPath, ex);
            return false;
        }

        return true;
    }
}
