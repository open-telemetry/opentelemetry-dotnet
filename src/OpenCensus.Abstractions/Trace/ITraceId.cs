// <copyright file="ITraceId.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace
{
    using System;

    /// <summary>
    /// Trace ID.
    /// </summary>
    public interface ITraceId : IComparable<ITraceId>
    {
        /// <summary>
        /// Gets the bytes representation of a trace id.
        /// </summary>
        byte[] Bytes { get; }

        /// <summary>
        /// Gets a value indicating whether trace if is valid.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Gets the lower long of the trace ID.
        /// </summary>
        long LowerLong { get; }

        /// <summary>
        /// Copy trace ID as bytes into the destination bytes array at a given offset.
        /// </summary>
        /// <param name="dest">Destination bytes array.</param>
        /// <param name="destOffset">Desitnation bytes array offset.</param>
        void CopyBytesTo(byte[] dest, int destOffset);

        /// <summary>
        /// Gets the lower base 16 representaiton of the trace id.
        /// </summary>
        /// <returns>Canonical string representation of a trace id.</returns>
        string ToLowerBase16();
    }
}
