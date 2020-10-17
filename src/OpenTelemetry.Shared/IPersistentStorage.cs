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
        /// Read a sequence of blobs from storage.
        /// </summary>
        /// <returns>Sequence of blobs from storage.</returns>
        public IEnumerable<IPersistentBlob> GetBlobs();

        /// <summary>
        /// Read a blob from storage.
        /// </summary>
        /// <returns>Blob.</returns>
        IPersistentBlob GetBlob();

        /// <summary>
        /// Write a blob to storage.
        /// </summary>
        /// <param name="persistentBlob">Blob.</param>
        /// <param name="leasePeriodMilliseconds">Lease period in milliseconds.</param>
        public void PutBlob(IPersistentBlob persistentBlob, int leasePeriodMilliseconds = 0);
    }
}
