// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
namespace OpenTelemetry.PersistentStorage.FileSystem;

/// <summary>
/// A <see cref="MemoryStream"/> that refuses to grow beyond a fixed size, so that the amount of memory
/// used for data that is produced from an untrusted source (such as a file that can be replaced by
/// another user) is bounded.
/// </summary>
internal sealed class BoundedMemoryStream : MemoryStream
{
    private readonly int maxLength;

    public BoundedMemoryStream(int maxLength)
    {
        this.maxLength = maxLength;
    }

    public override int Capacity
    {
        get => base.Capacity;

        // The stream grows by doubling its capacity, which could otherwise exceed the maximum length. A capacity
        // that is larger than the maximum is never needed as the length of the stream is limited to the maximum.
        set => base.Capacity = (int)Math.Min(value, this.maxLength);
    }

    public override void SetLength(long value)
    {
        this.EnsureWithinMaximum(value);
        base.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.EnsureCapacity(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.EnsureCapacity(buffer.Length);
        base.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        this.EnsureCapacity(1);
        base.WriteByte(value);
    }

    private void EnsureCapacity(int count) => this.EnsureWithinMaximum(this.Position + count);

    private void EnsureWithinMaximum(long length)
    {
        if (length > this.maxLength)
        {
            throw new IOException($"The data exceeds the maximum length of {this.maxLength} bytes.");
        }
    }
}
#endif
