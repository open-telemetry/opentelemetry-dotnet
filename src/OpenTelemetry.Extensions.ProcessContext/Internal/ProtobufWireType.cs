// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// Protobuf wire types used by <see cref="ProtobufWriter"/>.
/// </summary>
internal enum ProtobufWireType : uint
{
    /// <summary>Variable length integer.</summary>
    VARINT = 0,

    /// <summary>Fixed 64-bit value.</summary>
    I64 = 1,

    /// <summary>Length delimited value (string, bytes, nested message).</summary>
    LEN = 2,
}
