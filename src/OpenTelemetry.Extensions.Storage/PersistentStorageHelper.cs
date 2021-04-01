// <copyright file="PersistentStorageHelper.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenTelemetry.Extensions.Storage
{
    internal static class PersistentStorageHelper
    {
        private static long directorySize;

        internal static void RemoveExpiredBlob(DateTime retentionDeadline, string filePath)
        {
            if (filePath.EndsWith(".blob", StringComparison.OrdinalIgnoreCase))
            {
                DateTime fileDateTime = GetDateTimeFromBlobName(filePath);
                if (fileDateTime < retentionDeadline)
                {
                    try
                    {
                        File.Delete(filePath);
                        StorageEventSource.Log.Warning("File write exceeded retention. Dropping telemetry");
                    }
                    catch (Exception ex)
                    {
                        StorageEventSource.Log.Warning($"Deletion of file {filePath} has failed.", ex);
                    }
                }
            }
        }

        internal static bool RemoveExpiredLease(DateTime leaseDeadline, string filePath)
        {
            bool success = false;

            if (filePath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                DateTime fileDateTime = GetDateTimeFromLeaseName(filePath);
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
                        StorageEventSource.Log.Warning("File rename of {filePath} to {newFilePath} has failed.", ex);
                    }
                }
            }

            return success;
        }

        internal static bool RemoveTimedOutTmpFiles(DateTime timeoutDeadline, string filePath)
        {
            bool success = false;

            if (filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                DateTime fileDateTime = GetDateTimeFromBlobName(filePath);
                if (fileDateTime < timeoutDeadline)
                {
                    try
                    {
                        File.Delete(filePath);
                        success = true;
                        StorageEventSource.Log.Warning("File write exceeded timeout. Dropping telemetry");
                    }
                    catch (Exception ex)
                    {
                        StorageEventSource.Log.Warning($"Deletion of file {filePath} has failed.", ex);
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

            foreach (var file in Directory.EnumerateFiles(directoryPath).OrderByDescending(f => f))
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

            // It is faster to calculate the directory size, instead of removing length of expired files.
            var size = CalculateFolderSize(directoryPath);
            Interlocked.Exchange(ref directorySize, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long GetDirectorySize()
        {
            return Interlocked.Read(ref directorySize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteAllBytes(string path, byte[] buffer)
        {
            File.WriteAllBytes(path, buffer);
            UpdateDirectorySize(buffer.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveFile(string fileName)
        {
            var fileInfo = new FileInfo(fileName);
            var fileSize = fileInfo.Length;
            fileInfo.Delete();
            UpdateDirectorySize(fileSize * -1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string GetUniqueFileName(string extension)
        {
            return string.Format(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyy-MM-ddTHHmmss.fffffffZ}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}{extension}");
        }

        internal static string CreateSubdirectory(string path)
        {
            string subdirectoryPath = string.Empty;

            try
            {
                string baseDirectory = string.Empty;
#if !NETSTANDARD
                baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
                baseDirectory = AppContext.BaseDirectory;
#endif

                string appIdentity = Environment.UserName + "@" + Path.Combine(baseDirectory, Process.GetCurrentProcess().ProcessName);
                string subdirectoryName = GetSHA256Hash(appIdentity);
                subdirectoryPath = Path.Combine(path, subdirectoryName);
                Directory.CreateDirectory(subdirectoryPath);
            }
            catch (Exception ex)
            {
                StorageEventSource.Log.Error($"Error creating sub-directory {path}.", ex);
            }

            directorySize = CalculateFolderSize(subdirectoryPath);
            return subdirectoryPath;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long UpdateDirectorySize(long fileContentLength)
        {
            return Interlocked.Add(ref directorySize, fileContentLength);
        }

        internal static DateTime GetDateTimeFromBlobName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var time = fileName.Substring(0, fileName.LastIndexOf('-'));
            DateTime.TryParseExact(time, "yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
            return dateTime.ToUniversalTime();
        }

        internal static DateTime GetDateTimeFromLeaseName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var startIndex = fileName.LastIndexOf('@') + 1;
            var time = fileName.Substring(startIndex, fileName.Length - startIndex);
            DateTime.TryParseExact(time, "yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
            return dateTime.ToUniversalTime();
        }

        internal static string GetSHA256Hash(string input)
        {
            var hashString = new StringBuilder();

            byte[] inputBits = Encoding.Unicode.GetBytes(input);
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBits = sha256.ComputeHash(inputBits);
                foreach (byte b in hashBits)
                {
                    hashString.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
            }

            return hashString.ToString();
        }

        private static long CalculateFolderSize(string path)
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
                StorageEventSource.Log.Error("Error calculating folder size.", ex);
            }

            return directorySize;
        }
    }
}
