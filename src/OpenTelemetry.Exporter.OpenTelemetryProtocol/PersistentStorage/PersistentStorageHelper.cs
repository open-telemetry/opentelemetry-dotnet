// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
#if !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

namespace OpenTelemetry.PersistentStorage.FileSystem;

internal static class PersistentStorageHelper
{
    private const string BlobExtension = ".blob";
    private const string TimestampFormat = "yyyy-MM-ddTHHmmss.fffffffZ";

    internal static void RemoveExpiredBlob(DateTime retentionDeadline, string filePath)
    {
        if (filePath.EndsWith(BlobExtension, StringComparison.OrdinalIgnoreCase) && IsBlobFileName(Path.GetFileName(filePath)))
        {
            var fileDateTime = GetDateTimeFromBlobName(filePath);
            if (fileDateTime < retentionDeadline)
            {
                try
                {
                    File.Delete(filePath);
                    PersistentStorageEventSource.Log.PersistentStorageInformation(nameof(PersistentStorageHelper), "Removing blob as retention deadline expired");
                }
                catch (Exception ex)
                {
                    PersistentStorageEventSource.Log.CouldNotRemoveExpiredBlob(filePath, ex);
                }
            }
        }
    }

    internal static bool RemoveExpiredLease(DateTime leaseDeadline, string filePath)
    {
        var success = false;

        if (filePath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) && IsLeaseFileName(Path.GetFileName(filePath)))
        {
            var fileDateTime = GetDateTimeFromLeaseName(filePath);
            if (fileDateTime < leaseDeadline)
            {
                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileName(filePath);

                var atSignIndex = fileName.LastIndexOf('@');
                if (atSignIndex == -1)
                {
                    return false;
                }

                var newFileName = fileName.Substring(0, atSignIndex);
                var newFilePath = string.IsNullOrEmpty(directory)
                    ? newFileName
                    : Path.Combine(directory, newFileName);

                try
                {
                    File.Move(filePath, newFilePath);
                    success = true;
                }
                catch (Exception ex)
                {
                    PersistentStorageEventSource.Log.CouldNotRemoveExpiredLease(filePath, newFilePath, ex);
                }
            }
        }

        return success;
    }

    internal static bool RemoveTimedOutTmpFiles(DateTime timeoutDeadline, string filePath)
    {
        var success = false;

        if (filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) && IsTemporaryFileName(Path.GetFileName(filePath)))
        {
            var fileDateTime = GetDateTimeFromBlobName(filePath);
            if (fileDateTime < timeoutDeadline)
            {
                try
                {
                    File.Delete(filePath);
                    success = true;
                    PersistentStorageEventSource.Log.PersistentStorageInformation(nameof(PersistentStorageHelper), "File write exceeded timeout. Dropping telemetry");
                }
                catch (Exception ex)
                {
                    PersistentStorageEventSource.Log.CouldNotRemoveTimedOutTmpFile(filePath, ex);
                }
            }
        }

        return success;
    }

    internal static void RemoveExpiredBlobs(string directoryPath, long retentionPeriodInMilliseconds, long writeTimeoutInMilliseconds)
    {
        var currentUtcDateTime = DateTime.UtcNow;

        var leaseDeadline = currentUtcDateTime;
        var retentionDeadline = currentUtcDateTime - TimeSpan.FromMilliseconds(retentionPeriodInMilliseconds);
        var timeoutDeadline = currentUtcDateTime - TimeSpan.FromMilliseconds(writeTimeoutInMilliseconds);

        foreach (var file in Directory.EnumerateFiles(directoryPath).OrderByDescending(filename => filename))
        {
            var success = RemoveTimedOutTmpFiles(timeoutDeadline, file);

            if (success)
            {
                continue;
            }

            success = RemoveExpiredLease(leaseDeadline, file);

            if (!success)
            {
                RemoveExpiredBlob(retentionDeadline, file);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteAllBytes(string path, byte[] buffer)
        => File.WriteAllBytes(path, buffer);

    internal static void WriteAllBytes(string path, ReadOnlySpan<byte> buffer)
    {
#if NET
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(path, options);
        stream.Write(buffer);
#else
        File.WriteAllBytes(path, buffer.ToArray());
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RemoveFile(string fileName, out long fileSize)
    {
        var fileInfo = new FileInfo(fileName);
        fileSize = fileInfo.Length;
        fileInfo.Delete();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetUniqueFileName(string extension)
        => string.Format(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyy-MM-ddTHHmmss.fffffffZ}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}{extension}");

    /// <summary>
    /// Determines whether a file name is the name of a blob created by <see cref="FileBlobProvider"/>.
    /// </summary>
    /// <remarks>
    /// The storage directory can be shared with files that were not created by this component. Only files with the names
    /// this component gives its blobs (<c>{timestamp}-{guid}.blob</c>), and the temporary and lease files derived from them,
    /// may be removed or renamed when the storage is maintained.
    /// </remarks>
    /// <param name="fileName">The file name, without any directory.</param>
    /// <returns><see langword="true"/> if the file name is the name of a blob; otherwise <see langword="false"/>.</returns>
    internal static bool IsBlobFileName(string fileName)
    {
        if (!fileName.EndsWith(BlobExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var name = fileName.Substring(0, fileName.Length - BlobExtension.Length);
        var dashIndex = name.LastIndexOf('-');

        return dashIndex > 0
            && Guid.TryParseExact(name.Substring(dashIndex + 1), "N", out _)
            && TryParseTimestamp(name.Substring(0, dashIndex), out _);
    }

    internal static string CreateSubdirectory(string path)
    {
        try
        {
#if NET
            // The retry directory holds serialized telemetry that is later replayed to the collector
            // with the exporter's own credentials. Restrict it to the current user so other local
            // users cannot read the stored telemetry or plant blobs that would be sent on the app's
            // behalf. The mode is applied atomically when the directory is created, and the
            // permissions of a directory that already exists (for example one an operator has
            // deliberately configured and shared) are left unchanged. On Windows the created
            // directory inherits the parent ACL.
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
#else
            Directory.CreateDirectory(path);
#endif
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.PersistentStorageException(nameof(PersistentStorageHelper), $"Could not create directory {path}", ex);
            throw;
        }

        return path;
    }

    internal static DateTime GetDateTimeFromBlobName(string filePath)
    {
        var fileName = GetFileNameWithoutExtension(filePath);
        var dashIndex = fileName.LastIndexOf('-');
        if (dashIndex == -1)
        {
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }

        var timestamp = fileName.Substring(0, dashIndex);

        return Parse(timestamp);
    }

    internal static DateTime GetDateTimeFromLeaseName(string filePath)
    {
        var fileName = GetFileNameWithoutExtension(filePath);
        var startIndex = fileName.LastIndexOf('@') + 1;
        var timestamp = fileName.Substring(startIndex);

        return Parse(timestamp);
    }

    private static bool IsTemporaryFileName(string fileName)
        => IsBlobFileName(Path.GetFileNameWithoutExtension(fileName));

    private static bool IsLeaseFileName(string fileName)
    {
        // Lease files are named {blob}@{timestamp}.lock
        var name = Path.GetFileNameWithoutExtension(fileName);
        var atSignIndex = name.LastIndexOf('@');

        return atSignIndex > 0
            && IsBlobFileName(name.Substring(0, atSignIndex))
            && TryParseTimestamp(name.Substring(atSignIndex + 1), out _);
    }

    private static string GetFileNameWithoutExtension(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

#if !NETFRAMEWORK
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Non-Windows platforms will treat the entire path as the file name if it contains Windows
            // path separators, so we need to extract the file name manually from after the last \ character.
            var startIndex = fileName.LastIndexOf('\\');
            if (startIndex > -1)
            {
                fileName = fileName.Substring(startIndex + 1);
            }
        }
#endif

        return fileName;
    }

    private static bool TryParseTimestamp(string timestamp, out DateTime dateTime)
        => DateTime.TryParseExact(timestamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dateTime);

    private static DateTime Parse(string timestamp)
    {
        if (!TryParseTimestamp(timestamp, out var dateTime))
        {
            // In case of failure, return DateTime.MinValue so that the lease file can be removed as expired
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }

        return dateTime.ToUniversalTime();
    }
}
