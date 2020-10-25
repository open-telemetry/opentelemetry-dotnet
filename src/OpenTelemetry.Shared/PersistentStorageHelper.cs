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
using System.Security.Cryptography;
using System.Text;

namespace OpenTelemetry.Shared
{
    internal static class PersistentStorageHelper
    {
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
                        SharedEventSource.Log.Warning("File write exceeded retention. Dropping telemetry");
                    }
                    catch (Exception ex)
                    {
                        SharedEventSource.Log.Warning($"Deletion of file {filePath} has failed.", ex);
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
                        SharedEventSource.Log.Warning("File rename of {filePath} to {newFilePath} has failed.", ex);
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
                        SharedEventSource.Log.Warning("File write exceeded timeout. Dropping telemetry");
                    }
                    catch (Exception ex)
                    {
                        SharedEventSource.Log.Warning($"Deletion of file {filePath} has failed.", ex);
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
        }

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
                SharedEventSource.Log.Error($"Error creating sub-directory {path}.", ex);
            }

            return subdirectoryPath;
        }

        internal static float CalculateFolderSize(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            float directorySize = 0.0f;
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
                SharedEventSource.Log.Error("Error calculating folder size.", ex);
            }

            return directorySize;
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
    }
}
