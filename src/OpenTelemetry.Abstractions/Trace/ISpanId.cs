﻿// <copyright file="ISpanId.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System;

    /// <summary>
    /// Span identifier.
    /// </summary>
    public interface ISpanId : IComparable<ISpanId>
    {
        /// <summary>
        /// Gets the span identifier as bytes.
        /// </summary>
        byte[] Bytes { get; }

        /// <summary>
        /// Gets a value indicating whether span identifier is valid.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Copy span id as bytes into destination byte array.
        /// </summary>
        /// <param name="dest">Destination byte array.</param>
        /// <param name="destOffset">Offset to start writing from.</param>
        void CopyBytesTo(byte[] dest, int destOffset);

        /// <summary>
        /// Gets the span identifier as a string.
        /// </summary>
        /// <returns>String representation of Span identifier.</returns>
        string ToLowerBase16();
    }
}
