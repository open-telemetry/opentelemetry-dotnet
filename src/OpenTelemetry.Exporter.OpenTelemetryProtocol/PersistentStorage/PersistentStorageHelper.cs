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
    // The component only creates files with these lowercase extensions, so they are compared ordinally so that
    // files that only differ by case, which are distinct files on case-sensitive file systems, are left alone.
    private const string BlobExtension = ".blob";
    private const string LeaseExtension = ".lock";
    private const string TemporaryExtension = ".tmp";
    private const string TimestampFormat = "yyyy-MM-ddTHHmmss.fffffffZ";
    private const int GuidLength = 32;

    private static readonly TimeSpan MaximumPastTimestampOffset = TimeSpan.FromDays(100 * 365);
    private static readonly TimeSpan MaximumFutureTimestampOffset = TimeSpan.FromDays(365);

    internal static void RemoveExpiredBlob(DateTime retentionDeadline, string filePath)
    {
        if (filePath.EndsWith(BlobExtension, StringComparison.Ordinal) && IsBlobFileName(Path.GetFileName(filePath)))
        {
            var fileDateTime = GetBlobCreationTime(filePath);
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

        if (filePath.EndsWith(LeaseExtension, StringComparison.Ordinal) && IsLeaseFileName(Path.GetFileName(filePath)))
        {
            var fileDateTime = GetDateTimeFromLeaseName(filePath);

            // A lease can be held for at most int.MaxValue milliseconds (just under 25 days), so a lease that expires
            // further in the future than a legacy timestamp, such as because the clock has since been changed, has been
            // abandoned. Leases with legacy timestamps in the past have already expired.
            if (fileDateTime < leaseDeadline || IsFarFutureTimestamp(fileDateTime))
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

        if (filePath.EndsWith(TemporaryExtension, StringComparison.Ordinal) && IsTemporaryFileName(Path.GetFileName(filePath)))
        {
            var fileDateTime = GetDateTimeFromTemporaryFileName(filePath);
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

    /// <summary>
    /// Removes the file of a blob.
    /// </summary>
    /// <remarks>
    /// The file is claimed by atomically moving it to a new name before it is measured and removed. Otherwise the storage
    /// maintenance could release its lease between it being measured and removed, and as removing a file that does not
    /// exist succeeds, it would be reported as removed while still being left behind under its name without the lease.
    /// The new name is that of a temporary file, so if it cannot be removed the storage maintenance will remove it later.
    /// </remarks>
    /// <param name="filePath">The path of the file to remove.</param>
    /// <returns>The size of the file that was removed, or zero if it was claimed but could not be removed.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    internal static long RemoveClaimedFile(string filePath)
    {
        var claimedPath = GetUniqueFilePath(filePath, ".blob.tmp");

        File.Move(filePath, claimedPath);

        try
        {
            RemoveFile(claimedPath, out var fileSize);
            return fileSize;
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotDeleteFileBlob(claimedPath, ex);
            return 0;
        }
    }

    /// <summary>
    /// Removes the file of a blob whose content has been replaced by a file with a different name.
    /// </summary>
    /// <param name="filePath">The path of the file that was replaced.</param>
    /// <param name="destinationFilePath">The path of the file that replaced it.</param>
    /// <returns>The size of the file that was removed, or zero if no file was removed.</returns>
    internal static long RemoveReplacedFile(string filePath, string destinationFilePath)
    {
        try
        {
            return RemoveClaimedFile(filePath);
        }
        catch (FileNotFoundException)
        {
            // The lease may have expired and been released by the storage maintenance since the file was found, in which
            // case it needs to be removed using its name with one or more leases removed so that it is not left behind.
            // The leases are released one at a time and an existing file is never replaced when a lease is released,
            // so the file cannot have been released to or beyond the name that the new content was moved to.
            var destination = Path.GetFullPath(destinationFilePath);

            foreach (var releasedPath in GetReleasedPaths(filePath))
            {
                if (string.Equals(Path.GetFullPath(releasedPath), destination, StringComparison.Ordinal))
                {
                    break;
                }

                try
                {
                    return RemoveClaimedFile(releasedPath);
                }
                catch (FileNotFoundException)
                {
                    // Try the name with the next lease removed
                }
                catch (Exception ex)
                {
                    PersistentStorageEventSource.Log.CouldNotDeleteFileBlob(releasedPath, ex);
                    return 0;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            PersistentStorageEventSource.Log.CouldNotDeleteFileBlob(filePath, ex);
            return 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetUniqueFileName(string extension)
        => $"{FormatTimestamp(DateTime.UtcNow)}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}{extension}";

    /// <summary>
    /// Gets a path for a new file in the same directory as the specified file that is named the way temporary files are.
    /// </summary>
    /// <param name="filePath">The path of a file in the directory.</param>
    /// <param name="extension">The extension of the new file.</param>
    /// <returns>The path of the new file.</returns>
    internal static string GetUniqueFilePath(string filePath, string extension)
        => Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, GetUniqueFileName(extension));

    /// <summary>
    /// Formats a timestamp for use in the name of a file created by <see cref="FileBlobProvider"/>.
    /// </summary>
    /// <param name="timestamp">The UTC timestamp to format.</param>
    /// <returns>The formatted timestamp.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatTimestamp(DateTime timestamp)
        => timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);

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
        if (!fileName.EndsWith(BlobExtension, StringComparison.Ordinal))
        {
            return false;
        }

        var name = fileName.Substring(0, fileName.Length - BlobExtension.Length);
        var dashIndex = name.LastIndexOf('-');

        // Guid.TryParseExact() ignores leading and trailing whitespace and accepts uppercase hexadecimal digits,
        // so the GUID is matched exactly against the lowercase format that GetUniqueFileName() uses instead.
        return dashIndex > 0
            && name.Length - dashIndex - 1 == GuidLength
            && IsLowercaseHexadecimal(name, dashIndex + 1)
            && IsTimestamp(name.Substring(0, dashIndex));
    }

    internal static string CreateSubdirectory(string path)
    {
        try
        {
#if NET
            // The storage directory holds serialized telemetry that is later replayed by the exporter
            // with its own credentials. Restrict it to the current user so other local users cannot
            // read the stored telemetry or plant blobs that would be sent on the application's behalf.
            // The mode is applied atomically when the directory is created, and the permissions of a
            // directory that already exists (for example one an operator has deliberately configured
            // and shared) are left unchanged. On Windows the created directory inherits the parent ACL.
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

    /// <summary>
    /// Gets when a blob was created.
    /// </summary>
    /// <remarks>
    /// The creation time is determined from the blob's name, unless the name contains a legacy timestamp, in which
    /// case the time that the blob was last written to is used instead.
    /// </remarks>
    /// <param name="filePath">The path of the blob.</param>
    /// <returns>The UTC time that the blob was created.</returns>
    internal static DateTime GetBlobCreationTime(string filePath)
        => GetLastWriteTimeIfLegacy(filePath, GetDateTimeFromBlobName(filePath));

    /// <summary>
    /// Removes any leases from the name of a blob.
    /// </summary>
    /// <param name="filePath">The path of the blob, which may be leased.</param>
    /// <returns>The path of the blob without any leases.</returns>
    internal static string RemoveLeases(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var name = fileName;

        while (TryRemoveLease(name, out var withoutLease))
        {
            name = withoutLease;
        }

        if (string.Equals(name, fileName, StringComparison.Ordinal))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath);

        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }

    /// <summary>
    /// Gets the paths that a leased blob would have as each of its leases are released, from the last lease to the first.
    /// </summary>
    /// <param name="filePath">The path of the blob, which may be leased.</param>
    /// <returns>The paths of the blob with one or more of its leases removed, ending with its path without any leases.</returns>
    internal static List<string> GetReleasedPaths(string filePath)
    {
        var paths = new List<string>();
        var directory = Path.GetDirectoryName(filePath);
        var name = Path.GetFileName(filePath);

        while (TryRemoveLease(name, out var withoutLease))
        {
            name = withoutLease;
            paths.Add(string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name));
        }

        return paths;
    }

    internal static DateTime GetDateTimeFromLeaseName(string filePath)
    {
        var fileName = GetFileNameWithoutExtension(filePath);
        var startIndex = fileName.LastIndexOf('@') + 1;
        var timestamp = fileName.Substring(startIndex);

        return Parse(timestamp);
    }

    private static DateTime GetDateTimeFromTemporaryFileName(string filePath)
    {
        // Temporary files are given a new name with the time they were created, but previous versions named them after
        // the blob ({blob}.tmp), or after its lease ({blob}@{timestamp}.lock.tmp) when a leased blob was written to. The
        // write to a leased blob may still be in progress while the lease is held, so those temporary files are timed
        // out relative to when the lease expires rather than when the blob was created.
        var fileName = GetFileNameWithoutExtension(filePath);

        var timestamp = fileName.EndsWith(LeaseExtension, StringComparison.Ordinal)
            ? GetDateTimeFromLeaseName(fileName)
            : GetDateTimeFromBlobName(fileName);

        return GetLastWriteTimeIfLegacy(filePath, timestamp);
    }

    private static DateTime GetLastWriteTimeIfLegacy(string filePath, DateTime timestamp)
    {
        // DateTime.MinValue indicates that the name does not contain a timestamp at all
        if (timestamp == DateTime.MinValue || !IsLegacyTimestamp(timestamp))
        {
            return timestamp;
        }

        try
        {
            return File.GetLastWriteTimeUtc(filePath);
        }
        catch (Exception)
        {
            return timestamp;
        }
    }

    private static bool IsLegacyTimestamp(DateTime timestamp)
        => IsFarPastTimestamp(timestamp) || IsFarFutureTimestamp(timestamp);

    private static bool IsFarPastTimestamp(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        return now.Ticks > MaximumPastTimestampOffset.Ticks && timestamp < now - MaximumPastTimestampOffset;
    }

    private static bool IsFarFutureTimestamp(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        return now < DateTime.MaxValue - MaximumFutureTimestampOffset && timestamp > now + MaximumFutureTimestampOffset;
    }

    private static bool IsTemporaryFileName(string fileName)
    {
        if (!fileName.EndsWith(TemporaryExtension, StringComparison.Ordinal))
        {
            return false;
        }

        var name = fileName.Substring(0, fileName.Length - TemporaryExtension.Length);

        return IsBlobFileName(name) || IsLeaseFileName(name);
    }

    private static bool IsLeaseFileName(string fileName)
    {
        // Lease files are named {blob}@{timestamp}.lock. Writing to a blob that is already leased with
        // a new lease appends a further lease to its name, such as {blob}@{timestamp}.lock@{timestamp}.lock,
        // so every lease is validated back to the name of the blob that the leases were derived from.
        var name = fileName;
        var leases = 0;

        while (TryRemoveLease(name, out var withoutLease))
        {
            name = withoutLease;
            leases++;
        }

        return leases > 0 && IsBlobFileName(name);
    }

    private static bool TryRemoveLease(string fileName, out string withoutLease)
    {
        withoutLease = fileName;

        if (!fileName.EndsWith(LeaseExtension, StringComparison.Ordinal))
        {
            return false;
        }

        var name = fileName.Substring(0, fileName.Length - LeaseExtension.Length);
        var atSignIndex = name.LastIndexOf('@');

        if (atSignIndex <= 0 || !IsTimestamp(name.Substring(atSignIndex + 1)))
        {
            return false;
        }

        withoutLease = name.Substring(0, atSignIndex);
        return true;
    }

    private static bool IsLowercaseHexadecimal(string value, int startIndex)
    {
        for (var i = startIndex; i < value.Length; i++)
        {
            if (!char.IsAsciiHexDigitLower(value[i]))
            {
                return false;
            }
        }

        return true;
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

    private static bool IsTimestamp(string timestamp)
        => TryParseTimestamp(timestamp, out _);

    private static DateTime Parse(string timestamp)
    {
        if (TryParseTimestamp(timestamp, out var dateTime))
        {
            return dateTime.ToUniversalTime();
        }

        // In case of failure, return DateTime.MinValue so that the lease file can be removed as expired
        return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    }
}
