// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class CompositeActivityProcessorTests
{
    [Fact]
    public void CompositeActivityProcessor_BadArgs()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositeProcessor<Activity>(null!));
        Assert.Throws<ArgumentException>(() => new CompositeProcessor<Activity>(Array.Empty<BaseProcessor<Activity>>()));

        using var p1 = new TestActivityProcessor(null, null);
        using var processor = new CompositeProcessor<Activity>([p1]);
        Assert.Throws<ArgumentNullException>(() => processor.AddProcessor(null!));
    }

    [Fact]
    public void CompositeActivityProcessor_CallsAllProcessorSequentially()
    {
        var result = string.Empty;

        using var p1 = new TestActivityProcessor(
            activity => { result += "1"; },
            activity => { result += "3"; });
        using var p2 = new TestActivityProcessor(
            activity => { result += "2"; },
            activity => { result += "4"; });

        using var activity = new Activity("test");

        using (var processor = new CompositeProcessor<Activity>([p1, p2]))
        {
            processor.OnStart(activity);
            processor.OnEnd(activity);
        }

        Assert.Equal("1234", result);
    }

    [Fact]
    public void CompositeActivityProcessor_ProcessorThrows()
    {
        using var p1 = new TestActivityProcessor(
            activity => { throw new Exception("Start exception"); },
            activity => { throw new Exception("End exception"); });

        using var activity = new Activity("test");

        using var processor = new CompositeProcessor<Activity>([p1]);
        Assert.Throws<Exception>(() => { processor.OnStart(activity); });
        Assert.Throws<Exception>(() => { processor.OnEnd(activity); });
    }

    [Fact]
    public void CompositeActivityProcessor_ShutsDownAll()
    {
        using var p1 = new TestActivityProcessor(null, null);
        using var p2 = new TestActivityProcessor(null, null);

        using var processor = new CompositeProcessor<Activity>([p1, p2]);
        processor.Shutdown();
        Assert.True(p1.ShutdownCalled);
        Assert.True(p2.ShutdownCalled);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(0)]
    [InlineData(1)]
    public void CompositeActivityProcessor_ForceFlush(int timeout)
    {
        using var p1 = new TestActivityProcessor(null, null);
        using var p2 = new TestActivityProcessor(null, null);

        using var processor = new CompositeProcessor<Activity>([p1, p2]);
        processor.ForceFlush(timeout);

        Assert.True(p1.ForceFlushCalled);
        Assert.True(p2.ForceFlushCalled);
    }

    [Fact]
    public void CompositeActivityProcessor_ForwardsParentProvider()
    {
        using TracerProvider provider = new TestProvider();

        using var p1 = new TestActivityProcessor(null, null);
        using var p2 = new TestActivityProcessor(null, null);

        using var processor = new CompositeProcessor<Activity>([p1, p2]);

        Assert.Null(processor.ParentProvider);
        Assert.Null(p1.ParentProvider);
        Assert.Null(p2.ParentProvider);

        processor.SetParentProvider(provider);

        Assert.Equal(provider, processor.ParentProvider);
        Assert.Equal(provider, p1.ParentProvider);
        Assert.Equal(provider, p2.ParentProvider);
    }

    private sealed class TestProvider : TracerProvider
    {
    }
}
