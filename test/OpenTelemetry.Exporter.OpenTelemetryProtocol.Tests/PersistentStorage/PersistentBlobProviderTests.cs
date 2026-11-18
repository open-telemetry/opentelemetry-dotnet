// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

[Collection(EventSourceCollectionDefinition.Name)]
public class PersistentBlobProviderTests
{
    private static readonly byte[] Content = [1, 2, 3];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryCreateBlob_SpanWithLease_ForwardsContentAndLease(bool result)
    {
        var provider = new TestProvider { Result = result };

        Assert.Equal(result, provider.TryCreateBlob(new ReadOnlySpan<byte>(Content), 1234, out var blob));

        Assert.Equal(result, blob is not null);
        Assert.Equal(Content, provider.CreatedContent);
        Assert.NotSame(Content, provider.CreatedContent);
        Assert.Equal(1234, provider.CreatedLease);
    }

    [Fact]
    public void TryCreateBlob_SpanWithLease_ReturnsFalseWhenImplementationThrows()
    {
        var provider = new TestProvider { Throw = true };

        Assert.False(provider.TryCreateBlob(new ReadOnlySpan<byte>(Content), 1234, out var blob));
        Assert.Null(blob);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryCreateBlob_Span_ForwardsContent(bool result)
    {
        var provider = new TestProvider { Result = result };

        Assert.Equal(result, provider.TryCreateBlob(new ReadOnlySpan<byte>(Content), out var blob));

        Assert.Equal(result, blob is not null);
        Assert.Equal(Content, provider.CreatedContent);
        Assert.NotSame(Content, provider.CreatedContent);
        Assert.Null(provider.CreatedLease);
    }

    [Fact]
    public void TryCreateBlob_Span_ReturnsFalseWhenImplementationThrows()
    {
        var provider = new TestProvider { Throw = true };

        Assert.False(provider.TryCreateBlob(new ReadOnlySpan<byte>(Content), out var blob));
        Assert.Null(blob);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void TryCreateBlob_ByteArrayWithLease_ForwardsContentAndLease(bool result)
    {
        var provider = new TestProvider { Result = result };

        Assert.Equal(result, provider.TryCreateBlob(Content, 5678, out var blob));

        Assert.Equal(result, blob is not null);
        Assert.Same(Content, provider.CreatedContent);
        Assert.Equal(5678, provider.CreatedLease);
    }

    [Fact]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void TryCreateBlob_ByteArrayWithLease_ReturnsFalseWhenImplementationThrows()
    {
        var provider = new TestProvider { Throw = true };

        Assert.False(provider.TryCreateBlob(Content, 5678, out var blob));
        Assert.Null(blob);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void TryCreateBlob_ByteArray_ForwardsContent(bool result)
    {
        var provider = new TestProvider { Result = result };

        Assert.Equal(result, provider.TryCreateBlob(Content, out var blob));

        Assert.Equal(result, blob is not null);
        Assert.Same(Content, provider.CreatedContent);
        Assert.Null(provider.CreatedLease);
    }

    [Fact]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void TryCreateBlob_ByteArray_ReturnsFalseWhenImplementationThrows()
    {
        var provider = new TestProvider { Throw = true };

        Assert.False(provider.TryCreateBlob(Content, out var blob));
        Assert.Null(blob);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetBlob_ReturnsImplementationResult(bool result)
    {
        var provider = new TestProvider { Result = result };

        Assert.Equal(result, provider.TryGetBlob(out var blob));
        Assert.Equal(result, blob is not null);
    }

    [Fact]
    public void TryGetBlob_ReturnsFalseWhenImplementationThrows()
    {
        var provider = new TestProvider { Throw = true };

        Assert.False(provider.TryGetBlob(out var blob));
        Assert.Null(blob);
    }

    [Fact]
    public void GetBlobs_ReturnsImplementationResult()
    {
        var expected = new TestBlob();
        var provider = new TestProvider { Blobs = [expected] };

        var actual = Assert.Single(provider.GetBlobs());

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GetBlobs_ReturnsEmptyWhenImplementationReturnsNull()
    {
        var provider = new TestProvider { Blobs = null };

        Assert.Empty(provider.GetBlobs());
    }

    [Fact]
    public void GetBlobs_ReturnsEmptyWhenImplementationThrows()
    {
        var provider = new TestProvider { Throw = true };

        Assert.Empty(provider.GetBlobs());
    }

    [Fact]
    [Obsolete("Exercises the obsolete byte array overload.")]
    public void ImplementationExceptionsAreLogged()
    {
        using var listener = new TestEventListener();
        listener.EnableEvents(PersistentStorageAbstractionsEventSource.Log, EventLevel.Error, EventKeywords.All);

        var provider = new TestProvider { Throw = true };

        Assert.False(provider.TryCreateBlob(new ReadOnlySpan<byte>(Content), 100, out _));
        Assert.False(provider.TryCreateBlob(new ReadOnlySpan<byte>(Content), out _));
        Assert.False(provider.TryCreateBlob(Content, 200, out _));
        Assert.False(provider.TryCreateBlob(Content, out _));
        Assert.False(provider.TryGetBlob(out _));
        Assert.Empty(provider.GetBlobs());

        Assert.Equal(6, listener.Messages.ToList().Count);
    }

    private sealed class TestBlob : PersistentBlob
    {
        protected override bool OnTryRead([NotNullWhen(true)] out byte[]? buffer)
        {
            buffer = null;
            return false;
        }

        protected override bool OnTryWrite(byte[] buffer, int leasePeriodMilliseconds = 0) => false;

        protected override bool OnTryLease(int leasePeriodMilliseconds) => false;

        protected override bool OnTryDelete() => false;
    }

    private sealed class TestProvider : PersistentBlobProvider
    {
        public bool Result { get; set; }

        public bool Throw { get; set; }

        public IEnumerable<PersistentBlob>? Blobs { get; set; } = [];

        public byte[]? CreatedContent { get; private set; }

        public int? CreatedLease { get; private set; }

        protected override IEnumerable<PersistentBlob> OnGetBlobs()
        {
            this.ThrowIfRequested();

            return this.Blobs!;
        }

        protected override bool OnTryCreateBlob(byte[] buffer, int leasePeriodMilliseconds, [NotNullWhen(true)] out PersistentBlob? blob)
        {
            this.ThrowIfRequested();

            this.CreatedContent = buffer;
            this.CreatedLease = leasePeriodMilliseconds;
            blob = this.Result ? new TestBlob() : null;
            return this.Result;
        }

        protected override bool OnTryCreateBlob(byte[] buffer, [NotNullWhen(true)] out PersistentBlob? blob)
        {
            this.ThrowIfRequested();

            this.CreatedContent = buffer;
            this.CreatedLease = null;
            blob = this.Result ? new TestBlob() : null;
            return this.Result;
        }

        protected override bool OnTryGetBlob([NotNullWhen(true)] out PersistentBlob? blob)
        {
            this.ThrowIfRequested();

            blob = this.Result ? new TestBlob() : null;
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
