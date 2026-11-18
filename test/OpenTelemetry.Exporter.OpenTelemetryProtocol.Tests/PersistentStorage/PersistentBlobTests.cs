// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

[Collection(EventSourceCollectionDefinition.Name)]
public class PersistentBlobTests
{
    private static readonly byte[] Content = [1, 2, 3];

    [Fact]
    public void TryRead_ReturnsContentWhenSuccessful()
    {
        var blob = new TestBlob { Result = true, ReadContent = Content };

        Assert.True(blob.TryRead(out var buffer));
        Assert.Same(Content, buffer);
    }

    [Fact]
    public void TryRead_ReturnsFalseWhenUnsuccessful()
    {
        var blob = new TestBlob { Result = false };

        Assert.False(blob.TryRead(out var buffer));
        Assert.Null(buffer);
    }

    [Fact]
    public void TryRead_ReturnsFalseWhenImplementationThrows()
    {
        var blob = new TestBlob { Throw = true };

        Assert.False(blob.TryRead(out var buffer));
        Assert.Null(buffer);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryWrite_Span_ForwardsContentAndLease(bool result)
    {
        var blob = new TestBlob { Result = result };

        Assert.Equal(result, blob.TryWrite(new ReadOnlySpan<byte>(Content), 1234));

        Assert.Equal(Content, blob.WrittenContent);
        Assert.NotSame(Content, blob.WrittenContent);
        Assert.Equal(1234, blob.WrittenLease);
    }

    [Fact]
    public void TryWrite_Span_UsesDefaultLeaseOfZero()
    {
        var blob = new TestBlob { Result = true };

        Assert.True(blob.TryWrite(new ReadOnlySpan<byte>(Content)));

        Assert.Equal(0, blob.WrittenLease);
    }

    [Fact]
    public void TryWrite_Span_ReturnsFalseWhenImplementationThrows()
    {
        var blob = new TestBlob { Throw = true };

        Assert.False(blob.TryWrite(new ReadOnlySpan<byte>(Content), 1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void TryWrite_ByteArray_ForwardsContentAndLease(bool result)
    {
        var blob = new TestBlob { Result = result };

        Assert.Equal(result, blob.TryWrite(Content, 5678));

        Assert.Same(Content, blob.WrittenContent);
        Assert.Equal(5678, blob.WrittenLease);
    }

    [Fact]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void TryWrite_ByteArray_ReturnsFalseWhenImplementationThrows()
    {
        var blob = new TestBlob { Throw = true };

        Assert.False(blob.TryWrite(Content));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryLease_ForwardsLease(bool result)
    {
        var blob = new TestBlob { Result = result };

        Assert.Equal(result, blob.TryLease(42));

        Assert.Equal(42, blob.LeasePeriod);
    }

    [Fact]
    public void TryLease_ReturnsFalseWhenImplementationThrows()
    {
        var blob = new TestBlob { Throw = true };

        Assert.False(blob.TryLease(42));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryDelete_ReturnsImplementationResult(bool result)
    {
        var blob = new TestBlob { Result = result };

        Assert.Equal(result, blob.TryDelete());
    }

    [Fact]
    public void TryDelete_ReturnsFalseWhenImplementationThrows()
    {
        var blob = new TestBlob { Throw = true };

        Assert.False(blob.TryDelete());
    }

    [Fact]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void ImplementationExceptionsAreLogged()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(PersistentStorageAbstractionsEventSource.Log, EventLevel.Error, EventKeywords.All);

        var blob = new TestBlob { Throw = true };

        Assert.False(blob.TryRead(out _));
        Assert.False(blob.TryWrite(new ReadOnlySpan<byte>(Content)));
        Assert.False(blob.TryWrite(Content, 50));
        Assert.False(blob.TryLease(100));
        Assert.False(blob.TryDelete());

        Assert.Equal(5, listener.Messages.ToList().Count);
    }

    private sealed class TestBlob : PersistentBlob
    {
        public bool Result { get; set; }

        public bool Throw { get; set; }

        public byte[]? ReadContent { get; set; }

        public byte[]? WrittenContent { get; private set; }

        public int WrittenLease { get; private set; }

        public int LeasePeriod { get; private set; }

        protected override bool OnTryRead([NotNullWhen(true)] out byte[]? buffer)
        {
            this.ThrowIfRequested();

            buffer = this.Result ? this.ReadContent : null;
            return this.Result;
        }

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0)
        {
            this.ThrowIfRequested();

            this.WrittenContent = buffer;
            this.WrittenLease = leasePeriodMilliseconds;
            return this.Result;
        }

        protected override bool OnTryLease(int leasePeriodMilliseconds)
        {
            this.ThrowIfRequested();

            this.LeasePeriod = leasePeriodMilliseconds;
            return this.Result;
        }

        protected override bool OnTryDelete()
        {
            this.ThrowIfRequested();

            return this.Result;
        }

        private void ThrowIfRequested()
        {
            if (this.Throw)
            {
                throw new InvalidOperationException("Test exception.");
            }
        }
    }
}
