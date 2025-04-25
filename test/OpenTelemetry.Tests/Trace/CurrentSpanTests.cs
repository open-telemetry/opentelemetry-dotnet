// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public sealed class CurrentSpanTests : IDisposable
{
    private readonly Tracer tracer;

    public CurrentSpanTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        this.tracer = TracerProvider.Default.GetTracer(null!);
    }

    [Fact]
    public void CurrentSpan_WhenNoContext()
    {
        Assert.False(Tracer.CurrentSpan.Context.IsValid);
    }

    [Fact]
    public void CurrentSpan_WhenActivityExists()
    {
        using var activity = new Activity("foo").Start();
        Assert.True(Tracer.CurrentSpan.Context.IsValid);
    }

    public void Dispose()
    {
        Activity.Current = null;
        GC.SuppressFinalize(this);
    }
}
