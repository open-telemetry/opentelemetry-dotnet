﻿// <copyright file="ITagContextBinarySerializer.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
    /// Binary serializer and deserializer of tags.
    /// </summary>
    public interface ITagContextBinarySerializer
    {
        /// <summary>
        /// Converts tags into byte array.
        /// </summary>
        /// <param name="tags">Tags to serialize.</param>
        /// <returns>Binary representation of tags.</returns>
        byte[] ToByteArray(ITagContext tags);

        /// <summary>
        /// Deserialize tags from byte array.
        /// </summary>
        /// <param name="bytes">Bytes to deserialize.</param>
        /// <returns>Tags deserialized from bytes.</returns>
        ITagContext FromByteArray(byte[] bytes);
    }
}
