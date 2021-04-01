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

namespace OpenTelemetry.Extensions.Storage
{
    /// <summary>
    /// Persistent file storage <see cref="LocalFileStorage"/> allows to save data
    /// as blobs in file storage.
    /// </summary>
    public class LocalFileStorage : IPersistentStorage, IDisposable
    {
        private readonly string directoryPath;
        private readonly long maxSizeInBytes;
        private readonly long retentionPeriodInMilliseconds;
        private readonly int writeTimeoutInMilliseconds;
        private readonly Timer maintenanceTimer;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileStorage"/>
        /// class.
        /// </summary>
        /// <param name="path">
        /// Sets file storage folder location where blobs are stored.
        /// </param>
        /// <param name="maxSizeInBytes">
        /// Maximum allowed storage folder size.
        /// Default is 50 MB.
        /// </param>
        /// <param name="maintenancePeriodInMilliseconds">
        /// Maintenance event runs at specified interval.
        /// Removes expired leases and blobs that exceed retention period.
        /// Default is 2 minute.
        /// </param>
        /// <param name="retentionPeriodInMilliseconds">
        /// Retention period in milliseconds for the blob.
        /// Default is 2 days.
        /// </param>
        /// <param name="writeTimeoutInMilliseconds">
        /// Controls the timeout when writing a buffer to blob.
        /// Default is 1 minute.
        /// </param>
        public LocalFileStorage(
                                string path,
                                long maxSizeInBytes = 52428800,
                                int maintenancePeriodInMilliseconds = 6000,
                                long retentionPeriodInMilliseconds = 172800,
                                int writeTimeoutInMilliseconds = 6000)
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
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.maintenanceTimer.Dispose();
                }

                this.disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IPersistentBlob> GetBlobs()
        {
            var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(this.retentionPeriodInMilliseconds);

            foreach (var file in Directory.EnumerateFiles(this.directoryPath, "*.blob", SearchOption.TopDirectoryOnly).OrderByDescending(f => f))
            {
                DateTime fileDateTime = PersistentStorageHelper.GetDateTimeFromBlobName(file);
                if (fileDateTime > retentionDeadline)
                {
                    yield return new LocalFileBlob(file);
                }
            }
        }

        /// <inheritdoc/>
        public IPersistentBlob GetBlob()
        {
            return this.GetBlobs().FirstOrDefault();
        }

        /// <inheritdoc/>
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
                StorageEventSource.Log.Warning("CreateBlob has failed.", ex);
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
                StorageEventSource.Log.Error($"Error creating directory {this.directoryPath}", ex);
                return;
            }

            PersistentStorageHelper.RemoveExpiredBlobs(this.directoryPath, this.retentionPeriodInMilliseconds, this.writeTimeoutInMilliseconds);
        }

        private bool CheckStorageSize()
        {
            var size = PersistentStorageHelper.GetDirectorySize();
            if (size >= this.maxSizeInBytes)
            {
                StorageEventSource.Log.Warning($"Persistent storage max capacity has been reached. Currently at {size / 1024} KB. " +
                                                "Telemetry will be lost. Please consider increasing the value of storage max size in exporter config.");
                return false;
            }

            return true;
        }
    }
}
