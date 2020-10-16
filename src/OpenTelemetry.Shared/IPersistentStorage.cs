// <copyright file="IPersistentStorage.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Shared
{
    /// <summary>
    /// Persistent storage API.
    /// </summary>
    public interface IPersistentStorage
    {
        /// <summary>
        /// Reads all blobs from the storage.
        /// </summary>
        /// <returns>All blobs from storage.</returns>
        public IEnumerable<IPersistentBlob> GetBlobs();

        /// <summary>
        /// Reads the latest stored blob from the storage.
        /// </summary>
        /// <returns>Blob content.</returns>
        public IPersistentBlob GetBlob();

        /// <summary>
        /// Writes a buffer to storage.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        /// <param name="leasePeriod">Lease period in milliseconds.</param>
        public void PutBlob(byte[] buffer, int leasePeriod = 0);
    }
}
