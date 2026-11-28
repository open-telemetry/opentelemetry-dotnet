// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using OpenTelemetry.Extensions.ProcessContext.Internal;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

public sealed class ProcessContextHeaderTests : IDisposable
{
    private const long SampleAddress = 0x7f00_1234_5000;
    private const long PatternAddress = 0x0102030405060708;

    private readonly nint header = Marshal.AllocHGlobal(ProcessContextHeader.Size);

    public ProcessContextHeaderTests()
    {
        for (var i = 0; i < ProcessContextHeader.Size; i++)
        {
            Marshal.WriteByte(this.header, i, 0);
        }
    }

    [Fact]
    public void LayoutMatchesSpecification()
    {
        Assert.Equal(32, ProcessContextHeader.Size);
        Assert.Equal(0, ProcessContextHeader.SignatureOffset);
        Assert.Equal(8, ProcessContextHeader.VersionOffset);
        Assert.Equal(12, ProcessContextHeader.PayloadSizeOffset);
        Assert.Equal(16, ProcessContextHeader.PublishedAtOffset);
        Assert.Equal(24, ProcessContextHeader.PayloadPointerOffset);
        Assert.Equal(2, ProcessContextHeader.Version);
    }

    [Fact]
    public void PublishWritesEveryField()
    {
        ProcessContextHeader.Publish(this.header, payload: Address(SampleAddress), payloadSize: 77, publishedAtNanoseconds: 123_456_789);

        var read = HeaderReader.Read(this.header);
        Assert.Equal("OTEL_CTX", read.Signature);
        Assert.Equal(2u, read.Version);
        Assert.Equal(77u, read.PayloadSize);
        Assert.Equal(123_456_789UL, read.PublishedAt);
        Assert.Equal(Address(SampleAddress), read.Payload);
    }

    [Fact]
    public void PublishUsesNativeByteOrderWithoutPadding()
    {
        ProcessContextHeader.Publish(this.header, payload: Address(PatternAddress), payloadSize: 0x0A0B0C0D, publishedAtNanoseconds: 0x1112131415161718);

        var bytes = new byte[ProcessContextHeader.Size];
        Marshal.Copy(this.header, bytes, 0, bytes.Length);

        // x64 and arm64 Linux are little-endian.
        Assert.True(BitConverter.IsLittleEndian);
        Assert.Equal("OTEL_CTX"u8.ToArray(), bytes[0..8]);
        Assert.Equal(new byte[] { 2, 0, 0, 0 }, bytes[8..12]);
        Assert.Equal(new byte[] { 0x0D, 0x0C, 0x0B, 0x0A }, bytes[12..16]);
        Assert.Equal(new byte[] { 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11 }, bytes[16..24]);
        Assert.Equal(new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }, bytes[24..32]);
    }

    [Fact]
    public void UpdateZeroesTimestampWhileMutating()
    {
        ProcessContextHeader.Publish(this.header, payload: 0x1000, payloadSize: 10, publishedAtNanoseconds: 500);

        HeaderReader? duringUpdate = null;
        ProcessContextHeader.Update(this.header, payload: 0x2000, payloadSize: 20, publishedAtNanoseconds: 600, onMutating: () => duringUpdate = HeaderReader.Read(this.header));

        Assert.NotNull(duringUpdate);
        Assert.Equal(0UL, duringUpdate.Value.PublishedAt);

        // The payload fields are only swapped after the timestamp is zeroed.
        Assert.Equal((nint)0x1000, duringUpdate.Value.Payload);
        Assert.Equal(10u, duringUpdate.Value.PayloadSize);

        var after = HeaderReader.Read(this.header);
        Assert.Equal(600UL, after.PublishedAt);
        Assert.Equal((nint)0x2000, after.Payload);
        Assert.Equal(20u, after.PayloadSize);
        Assert.Equal("OTEL_CTX", after.Signature);
        Assert.Equal(2u, after.Version);
    }

    [Fact]
    public void InvalidateZeroesTimestamp()
    {
        ProcessContextHeader.Publish(this.header, payload: 0x1000, payloadSize: 10, publishedAtNanoseconds: 500);

        ProcessContextHeader.Invalidate(this.header);

        Assert.Equal(0UL, ProcessContextHeader.ReadPublishedAt(this.header));
    }

    public void Dispose() => Marshal.FreeHGlobal(this.header);

    // A runtime (not constant) conversion, which the compiler accepts for 64-bit values.
    private static nint Address(long value) => (nint)value;
}
