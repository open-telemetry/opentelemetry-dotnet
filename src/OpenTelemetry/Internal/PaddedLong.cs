// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Internal;

/// <summary>
/// A <see langword="long"/> with 64 bytes of padding on either side so that it
/// occupies its own cache line (and stays off the adjacent line the hardware
/// prefetcher pairs with it) regardless of how the runtime lays out the
/// surrounding fields. Used for values which different threads write
/// concurrently and which would otherwise falsely share a line.
/// </summary>
/// <remarks>
/// This must stay a non-generic type: the runtime rejects explicit layout on
/// generic types, including structs nested in generic classes.
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct PaddedLong
{
    [FieldOffset(64)]
    public long Value;
}
