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
using System.IO;
using System.Linq;
using System.Timers;

namespace OpenTelemetry.Shared
{
    public class LocalFileStorage : IPersistentStorage, IDisposable
    {
        private readonly string directoryPath;
        private readonly long maxSizeInBytes;
        private readonly long retentionPeriodInMilliseconds;
        private readonly int writeTimeoutInMilliseconds;
        private readonly Timer maintenanceTimer;

        public LocalFileStorage(
                                string path,
                                long maxSizeInBytes = 52428800,              // 50 MB
                                int maintenancePeriodInMilliseconds = 6000,  // 1 Minute
                                long retentionPeriodInMilliseconds = 172800, // 2 Days
                                int writeTimeoutInMilliseconds = 6000) // 1 Minute
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.directoryPath = PersistentStorageHelper.CreateSubdirectory(path);
            this.maxSizeInBytes = maxSizeInBytes;
            this.retentionPeriodInMilliseconds = retentionPeriodInMilliseconds;
            this.writeTimeoutInMilliseconds = writeTimeoutInMilliseconds;

            this.maintenanceTimer = new Timer(maintenancePeriodInMilliseconds);
            this.maintenanceTimer.Elapsed += this.OnMaintenanceEvent;
            this.maintenanceTimer.AutoReset = true;
            this.maintenanceTimer.Enabled = true;
        }

        public void Dispose()
        {
            this.maintenanceTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        public IEnumerable<IPersistentBlob> GetBlobs()
        {
            var retentionDeadline = DateTime.Now.ToUniversalTime() - TimeSpan.FromMilliseconds(this.retentionPeriodInMilliseconds);

            foreach (var file in Directory.GetFiles(this.directoryPath).OrderByDescending(f => f))
            {
                if (file.EndsWith(".blob", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime fileDateTime = PersistentStorageHelper.GetDateTimeFromBlobName(file);
                    if (fileDateTime > retentionDeadline)
                    {
                        yield return new LocalFileBlob(file);
                    }
                }
            }
        }

        public IPersistentBlob GetBlob()
        {
            var iterator = this.GetBlobs().GetEnumerator();
            iterator.MoveNext();
            return iterator.Current;
        }

        public IPersistentBlob CreateBlob(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            if (!this.CheckStorageSize())
            {
                return null;
            }

            try
            {
                var blobFilePath = Path.Combine(this.directoryPath, PersistentStorageHelper.GetUniqueFileName(".blob"));
                var blob = new LocalFileBlob(blobFilePath);
                return blob.Write(buffer, leasePeriodMilliseconds);
            }
            catch (Exception ex)
            {
                SharedEventSource.Log.Warning("CreateBlob has failed.", ex);
                return null;
            }
        }

        private void OnMaintenanceEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(this.directoryPath))
                {
                    Directory.CreateDirectory(this.directoryPath);
                }
            }
            catch (Exception ex)
            {
                SharedEventSource.Log.Error($"Error creating directory {this.directoryPath}", ex);
                return;
            }

            PersistentStorageHelper.RemoveExpiredBlobs(this.directoryPath, this.retentionPeriodInMilliseconds, this.writeTimeoutInMilliseconds);
        }

        private bool CheckStorageSize()
        {
            var size = PersistentStorageHelper.CalculateFolderSize(this.directoryPath);
            if (size >= this.maxSizeInBytes)
            {
                SharedEventSource.Log.Warning($"Persistent storage max capacity has been reached. Currently at {size / 1024}KB. " +
                                                "Telemetry will be lost. Please consider increasing the value of storage_max_size in exporter config.");
                return false;
            }

            return true;
        }
    }
}
