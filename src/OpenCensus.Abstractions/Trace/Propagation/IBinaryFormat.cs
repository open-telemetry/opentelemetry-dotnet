// <copyright file="IBinaryFormat.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Propagation
{
    /// <summary>
    /// Binary format propagator.
    /// </summary>
    public interface IBinaryFormat
    {
        /// <summary>
        /// Deserializes span context from the bytes array.
        /// </summary>
        /// <param name="bytes">Bytes array with the envoded span context in it.</param>
        /// <returns>Span context deserialized from the byte array.</returns>
        ISpanContext FromByteArray(byte[] bytes);

        /// <summary>
        /// Serialize span context into the bytes array.
        /// </summary>
        /// <param name="spanContext">Span context to serialize.</param>
        /// <returns>Byte array with the encoded span context in it.</returns>
        byte[] ToByteArray(ISpanContext spanContext);
    }
}
