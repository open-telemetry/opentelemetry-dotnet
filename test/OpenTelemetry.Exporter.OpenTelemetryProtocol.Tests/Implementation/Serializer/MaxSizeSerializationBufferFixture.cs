// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

/// <summary>
/// Owns the serialization buffer shared by the tests that need
/// <c>ProtobufSerializer.IncreaseBufferSize</c> to refuse to grow, together with
/// the maximum size those tests must pass to the serializer for the refusal to
/// happen.
/// </summary>
#pragma warning disable CA1515 // Consider making public types internal - required by xunit
public sealed class MaxSizeSerializationBufferFixture
#pragma warning restore CA1515 // Consider making public types internal - required by xunit
{
    /// <summary>
    /// The buffer is handed to the serializer along with this as the maximum
    /// size, so the buffer is already at its limit and <c>IncreaseBufferSize</c>
    /// gives up immediately, making <c>TryWriteResource*</c> rethrow the
    /// underlying serialization failure. Any size works, so this is kept small.
    /// </summary>
    internal const int MaxBufferSize = 256 * 1024 * 1024;

    private readonly byte[] buffer = new byte[MaxBufferSize];

    internal byte[] GetBuffer() => this.buffer;

    /// <summary>
    /// Sharing the buffer is only safe because the failure happens on the first
    /// write, before <c>IncreaseBufferSize</c> can swap in a new array, so nothing
    /// is written to it and the reference the serializer holds is unchanged.
    /// </summary>
    /// <param name="candidate">The buffer the serializer was handed.</param>
    internal void AssertNotSwapped(byte[] candidate) => Assert.Same(this.buffer, candidate);
}
