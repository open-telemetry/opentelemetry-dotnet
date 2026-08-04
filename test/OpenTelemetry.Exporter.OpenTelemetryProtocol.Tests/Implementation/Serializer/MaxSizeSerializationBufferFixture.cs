// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

/// <summary>
/// Owns the single maximum-size serialization buffer shared by the tests that need
/// <c>ProtobufSerializer.IncreaseBufferSize</c> to refuse to grow. Allocating it
/// once per test class - rather than once per test - keeps the cost to one 100 MiB
/// array per target framework, released when the class finishes.
/// </summary>
#pragma warning disable CA1515 // Consider making public types internal - required by xunit
public sealed class MaxSizeSerializationBufferFixture
#pragma warning restore CA1515 // Consider making public types internal - required by xunit
{
    // Mirrors the private ProtobufSerializer.MaxBufferSize. A buffer of this
    // length is the documented point at which IncreaseBufferSize gives up and
    // TryWriteResource* rethrows the underlying serialization failure.
    private const int MaxBufferSize = 100 * 1024 * 1024;

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
