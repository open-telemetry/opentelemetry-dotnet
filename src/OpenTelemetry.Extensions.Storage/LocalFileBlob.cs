// <copyright file="LocalFileBlob.cs" company="OpenTelemetry Authors">
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
using System.IO;

namespace OpenTelemetry.Extensions.Storage
{
    /// <summary>
    /// The <see cref="LocalFileBlob"/> allows to save a blob
    /// in file storage.
    /// </summary>
    public class LocalFileBlob : IPersistentBlob
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileBlob"/>
        /// class.
        /// </summary>
        /// <param name="fullPath">Absolute file path of the blob.</param>
        public LocalFileBlob(string fullPath)
        {
            this.FullPath = fullPath;
        }

        public string FullPath { get; private set; }

        /// <inheritdoc/>
        public byte[] Read()
        {
            try
            {
                return File.ReadAllBytes(this.FullPath);
            }
            catch (Exception ex)
            {
                StorageEventSource.Log.Warning($"Reading a blob from file {this.FullPath} has failed.", ex);
            }

            return null;
        }

        /// <inheritdoc/>
        public IPersistentBlob Write(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            string path = this.FullPath + ".tmp";

            try
            {
                File.WriteAllBytes(path, buffer);

                if (leasePeriodMilliseconds > 0)
                {
                    var timestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
                    this.FullPath += $"@{timestamp:yyyy-MM-ddTHHmmss.fffffffZ}.lock";
                }

                File.Move(path, this.FullPath);
            }
            catch (Exception ex)
            {
                StorageEventSource.Log.Warning($"Writing a blob to file {path} has failed.", ex);
            }

            return this;
        }

        /// <inheritdoc/>
        public IPersistentBlob Lease(int leasePeriodMilliseconds)
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
                StorageEventSource.Log.Warning($"Acquiring a lease to file {this.FullPath} has failed.", ex);
            }

            this.FullPath = path;
            return this;
        }

        /// <inheritdoc/>
        public void Delete()
        {
            try
            {
                File.Delete(this.FullPath);
            }
            catch (Exception ex)
            {
                StorageEventSource.Log.Warning($"Deletion of file blob {this.FullPath} has failed.", ex);
            }
        }
    }
}
