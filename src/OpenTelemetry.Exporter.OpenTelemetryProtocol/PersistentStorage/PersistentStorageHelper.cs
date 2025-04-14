// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;

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
                var newFilePath = filePath.Substring(0, filePath.LastIndexOf('@'));
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
    {
        File.WriteAllBytes(path, buffer);
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
    {
        return string.Format(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyy-MM-ddTHHmmss.fffffffZ}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}{extension}");
    }

    internal static string CreateSubdirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    internal static DateTime GetDateTimeFromBlobName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var time = fileName.Substring(0, fileName.LastIndexOf('-'));

        // TODO:Handle possible parsing failure.
        DateTime.TryParseExact(time, "yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
        return dateTime.ToUniversalTime();
    }

    internal static DateTime GetDateTimeFromLeaseName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var startIndex = fileName.LastIndexOf('@') + 1;
        var time = fileName.Substring(startIndex);
        DateTime.TryParseExact(time, "yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
        return dateTime.ToUniversalTime();
    }
}
