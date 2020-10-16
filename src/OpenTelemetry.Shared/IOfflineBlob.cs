// <copyright file="IOfflineBlob.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Shared
{
    /// <summary>
    /// API to Read, Write and Delete blob.
    /// </summary>
    public interface IOfflineBlob
    {
        /// <summary>
        /// Read content of a blob from storage.
        /// </summary>
        /// <returns>Blob content.</returns>
        public byte[] Read();

        /// <summary>
        /// Write blob content to storage.
        /// </summary>
        /// <param name="buffer">Telemetry buffer to write to storage.</param>
        /// <param name="leasePeriod">Lease period in seconds.</param>
        public void Write(byte[] buffer, int leasePeriod = 0);

        /// <summary>
        /// Create and manage a lock on a file for write and delete operations.
        /// </summary>
        /// <param name="seconds">Lease period in seconds.</param>
        public void Lease(int seconds);

        /// <summary>
        /// Delete a blob from storage.
        /// </summary>
        public void Delete();
    }
}
