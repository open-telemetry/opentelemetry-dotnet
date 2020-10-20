// <copyright file="IPersistentBlob.cs" company="OpenTelemetry Authors">
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
    /// Represents a persistent blob.
    /// </summary>
    public interface IPersistentBlob
    {
        /// <summary>
        /// Reads the content from the blob.
        /// </summary>
        /// <returns>
        /// The content of the blob if the operation succeeded, otherwise null.
        /// </returns>
        /// <remarks>
        /// This function should never throw exception.
        /// </remarks>
        byte[] Read();

        /// <summary>
        /// Writes the given content to the blob.
        /// </summary>
        /// <param name="buffer">
        /// The content to be written.
        /// </param>
        /// <param name="leasePeriodMilliseconds">
        /// The number of milliseconds to lease after the write operation finished.
        /// </param>
        /// <returns>
        /// The same blob if the operation succeeded, otherwise null.
        /// </returns>
        /// <remarks>
        /// This function should never throw exception.
        /// </remarks>
        IPersistentBlob Write(byte[] buffer, int leasePeriodMilliseconds = 0);

        /// <summary>
        /// Creates a lease on the blob.
        /// </summary>
        /// <param name="leasePeriodMilliseconds">
        /// The number of milliseconds to lease.
        /// </param>
        /// <returns>
        /// The same blob if the lease operation succeeded, otherwise null.
        /// </returns>
        /// <remarks>
        /// This function should never throw exception.
        /// </remarks>
        IPersistentBlob Lease(int leasePeriodMilliseconds);

        /// <summary>
        /// Attempts to delete the blob.
        /// </summary>
        /// <remarks>
        /// This function should never throw exception.
        /// </remarks>
        void Delete();
    }
}
