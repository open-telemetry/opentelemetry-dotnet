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

namespace OpenTelemetry.Shared
{
    public class LocalFileBlob : IPersistentBlob
    {
        public LocalFileBlob(string fullPath)
        {
            this.FullPath = fullPath;
        }

        public string FullPath { get; private set; }

        public byte[] Read()
        {
            try
            {
                return File.ReadAllBytes(this.FullPath);
            }
            catch (Exception ex)
            {
                SharedEventSource.Log.Warning($"Reading a blob from file {this.FullPath} has failed.", ex);
            }

            return null;
        }

        public IPersistentBlob Write(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            string path = this.FullPath + ".tmp";

            try
            {
                File.WriteAllBytes(path, buffer);

                if (leasePeriodMilliseconds > 0)
                {
                    var timestamp = DateTime.Now.ToUniversalTime() + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
                    this.FullPath += $"@{timestamp:yyy-MM-ddTHHmmss.ffffff}.lock";
                }

                File.Move(path, this.FullPath);
            }
            catch (Exception ex)
            {
                SharedEventSource.Log.Warning($"Writing a blob to file {path} has failed.", ex);
            }

            return this;
        }

        public IPersistentBlob Lease(int leasePeriodMilliseconds)
        {
            var path = this.FullPath;
            var leaseTimestamp = DateTime.Now.ToUniversalTime() + TimeSpan.FromMilliseconds(leasePeriodMilliseconds);
            if (path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.LastIndexOf('@'));
            }

            path += $"@{leaseTimestamp:yyy-MM-ddTHHmmss.ffffff}.lock";

            try
            {
                File.Move(this.FullPath, path);
            }
            catch (Exception ex)
            {
                SharedEventSource.Log.Warning($"Acquiring a lease to file {this.FullPath} has failed.", ex);
            }

            this.FullPath = path;
            return this;
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(this.FullPath))
                {
                    File.Delete(this.FullPath);
                }
            }
            catch (Exception ex)
            {
                SharedEventSource.Log.Warning($"Deletion of file blob {this.FullPath} has failed.", ex);
            }
        }
    }
}
