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
    internal static void RemoveExpiredBlob(DateTime retentionDeadline, string filePath)
    {
        if (filePath.EndsWith(".blob", StringComparison.OrdinalIgnoreCase))
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

        if (filePath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
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

        if (filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
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

    internal static string CreateSubdirectory(string path)
    {
#if NET
        // The retry directory holds serialized telemetry that is later replayed to the collector
        // with the exporter's own credentials. Restrict it to the current user so other local
        // users cannot read the stored telemetry or plant blobs that would be sent on the app's
        // behalf. Only tighten a directory this call actually creates, to avoid changing the
        // permissions of a directory an operator has deliberately configured and shared. On
        // Windows the created directory inherits the parent ACL.
        var created = !Directory.Exists(path);
        Directory.CreateDirectory(path);

        if (created && !OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception ex)
            {
                PersistentStorageEventSource.Log.PersistentStorageException(nameof(PersistentStorageHelper), $"Could not restrict permissions on directory {path}", ex);
            }
        }

        return path;
#else
        Directory.CreateDirectory(path);
        return path;
#endif
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

    private static DateTime Parse(string timestamp)
    {
        if (!DateTime.TryParseExact(timestamp, "yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTime))
        {
            // In case of failure, return DateTime.MinValue so that the lease file can be removed as expired
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }

        return dateTime.ToUniversalTime();
    }
}
