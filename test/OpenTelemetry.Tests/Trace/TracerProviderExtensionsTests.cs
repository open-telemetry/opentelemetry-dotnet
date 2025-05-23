// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;

using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TracerProviderExtensionsTests
{
    [Fact]
    public void Verify_ForceFlush_HandlesException()
    {
        using var testProcessor = new DelegatingProcessor<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(testProcessor)
            .Build();

        Assert.True(tracerProvider.ForceFlush());

        testProcessor.OnForceFlushFunc = _ => throw new InvalidOperationException("test exception");

        Assert.False(tracerProvider.ForceFlush());
    }

    [Fact]
    public void Verify_Shutdown_HandlesSecond()
    {
        using var testProcessor = new DelegatingProcessor<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(testProcessor)
            .Build();

        Assert.True(tracerProvider.Shutdown());
        Assert.False(tracerProvider.Shutdown());
    }

    [Fact]
    public void Verify_Shutdown_HandlesException()
    {
        using var testProcessor = new DelegatingProcessor<Activity>
        {
            OnShutdownFunc = _ => throw new InvalidOperationException("test exception"),
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(testProcessor)
            .Build();

        Assert.False(tracerProvider.Shutdown());
    }
}
