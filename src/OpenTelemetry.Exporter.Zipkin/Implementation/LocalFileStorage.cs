// <copyright file="LocalFileStorage.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal class LocalFileStorage : IDisposable
    {
        private readonly string path;
        private readonly long maxSize;
        private readonly int maintenancePeriod;
        private readonly long retentionPeriod;
        private readonly int writeTimeout;
        private readonly Thread maintenanceThread;
        private readonly AutoResetEvent maintenanceTrigger = new AutoResetEvent(false);

        internal LocalFileStorage(
                                string path,
                                long maxSize = 52428800,
                                int maintenancePeriod = 60,
                                long retentionPeriod = 172800,
                                int writeTimeout = 60)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.path = this.CreateTelemetrySubdirectory(path);
            this.maxSize = maxSize;
            this.maintenancePeriod = maintenancePeriod;
            this.retentionPeriod = retentionPeriod;
            this.writeTimeout = writeTimeout;
            this.maintenanceThread = new Thread(new ThreadStart(this.MaintenaceProc))
            {
                IsBackground = true,
                Name = $"OpenTelemetry-{nameof(LocalFileStorage)}",
            };
            this.maintenanceThread.Start();
        }

        public void Dispose()
        {
            this.maintenanceTrigger.Set();
            this.maintenanceThread.Join();
        }

        internal IEnumerable<LocalFileBlob> GetBlobs()
        {
            var currentUtcDateTime = DateTime.Now.ToUniversalTime();

            var leaseDeadline = currentUtcDateTime;
            var retentionDeadline = currentUtcDateTime - TimeSpan.FromSeconds(this.retentionPeriod);
            var timeoutDeadline = currentUtcDateTime - TimeSpan.FromSeconds(this.writeTimeout);

            foreach (var file in Directory.GetFiles(this.path))
            {
                var filePath = file;
                if (filePath.EndsWith(".tmp"))
                {
                    DateTime fileDateTime = GetDateTimeFromFileName(filePath, '-');
                    if (fileDateTime < timeoutDeadline)
                    {
                        this.DeleteFile(filePath);
                    }
                }

                if (filePath.EndsWith(".lock"))
                {
                    DateTime fileDateTime = GetDateTimeFromFileName(filePath, '@');
                    if (fileDateTime > leaseDeadline)
                    {
                        continue;
                    }

                    var newFilePath = filePath.Substring(0, filePath.LastIndexOf('@'));
                    try
                    {
                        File.Move(filePath, newFilePath);
                    }
                    catch (Exception)
                    {
                        // TODO: Log Exception
                        newFilePath = filePath;
                    }

                    filePath = newFilePath;
                }

                if (filePath.EndsWith(".blob"))
                {
                    DateTime fileDateTime = GetDateTimeFromFileName(filePath, '-');
                    if (fileDateTime < retentionDeadline)
                    {
                        this.DeleteFile(filePath);
                    }
                    else
                    {
                        yield return new LocalFileBlob(filePath);
                    }
                }
            }
        }

        internal LocalFileBlob GetBlob()
        {
            var iterator = this.GetBlobs().GetEnumerator();
            iterator.MoveNext();
            return iterator.Current;
        }

        internal void PutBlob(string[] data, int leasePeriod = 0)
        {
            if (!this.CheckStorageSize())
            {
                // TODO: Log Error
                return;
            }

            var blob = new LocalFileBlob(Path.Combine(this.path, this.GetUniqueFileName(".blob")));
            blob.Write(data, leasePeriod);
        }

        private static DateTime GetDateTimeFromFileName(string filePath, char seperator)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var time = fileName.Substring(0, fileName.LastIndexOf(seperator));
            DateTime.TryParseExact(time, "yyyy-MM-ddTHHmmss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
            return dateTime;
        }

        private string GetUniqueFileName(string extension)
        {
            string fileName = string.Format($"{DateTime.Now.ToUniversalTime():yyy-MM-ddTHHmmss.ffffff}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.blob");
            return fileName;
        }

        private void MaintenaceProc()
        {
            while (!this.maintenanceTrigger.WaitOne(this.maintenancePeriod))
            {
                try
                {
                    if (!Directory.Exists(this.path))
                    {
                        Directory.CreateDirectory(this.path);
                    }
                }
                catch (Exception)
                {
                    // TODO: Log Exception
                }

                try
                {
                    foreach (var blobItem in this.GetBlobs())
                    {
                    }
                }
                catch (Exception)
                {
                    // TODO: Log Exception
                }
            }
        }

        private bool CheckStorageSize()
        {
            var size = this.CalculateFolderSize(this.path);
            if (size >= this.maxSize)
            {
                // TODO: Log Error
                return false;
            }

            return true;
        }

        private float CalculateFolderSize(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            float directorySize = 0.0f;
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    if (File.Exists(file))
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        directorySize += fileInfo.Length;
                    }
                }

                foreach (string dir in Directory.GetDirectories(path))
                {
                    directorySize += this.CalculateFolderSize(dir);
                }
            }
            catch (Exception)
            {
                // TODO: Log Exception
            }

            return directorySize;
        }

        private string CreateTelemetrySubdirectory(string path)
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
                string subdirectoryName = this.GetSHA256Hash(appIdentity);
                subdirectoryPath = Path.Combine(path, subdirectoryName);
                Directory.CreateDirectory(subdirectoryPath);
            }
            catch (Exception)
            {
                // TODO: Log Exception
            }

            return subdirectoryPath;
        }

        private string GetSHA256Hash(string input)
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

        private void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // TODO: Log Exception
            }
        }
    }
}
