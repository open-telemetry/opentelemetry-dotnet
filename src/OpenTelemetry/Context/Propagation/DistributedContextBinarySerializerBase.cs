// <copyright file="DistributedContextBinarySerializerBase.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// DistributedContextBinarySerializerBase base class.
    /// </summary>
    public abstract class DistributedContextBinarySerializerBase
    {
        /// <summary>
        /// Deserializes input to <see cref="DistributedContext"/> based on the binary format standard.
        /// </summary>
        /// <param name="bytes">Array of bytes in binary format standard.</param>
        /// <returns><see cref="DistributedContext"/>.</returns>
        public abstract DistributedContext FromByteArray(byte[] bytes);

        /// <summary>
        /// Serializes a <see cref="DistributedContext"/> to the on-the-wire format.
        /// </summary>
        /// <param name="tags"><see cref="DistributedContext"/>.</param>
        /// <returns>Serialized <see cref="DistributedContext"/>.</returns>
        public abstract byte[] ToByteArray(DistributedContext tags);
    }
}
