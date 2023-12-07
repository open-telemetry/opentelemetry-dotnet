// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

public class SpanContextShimTests
{
    [Fact]
    public void GetTraceId()
    {
        var shim = GetSpanContextShim();

        Assert.Equal(shim.TraceId.ToString(), shim.TraceId);
    }

    [Fact]
    public void GetSpanId()
    {
        var shim = GetSpanContextShim();

        Assert.Equal(shim.SpanId.ToString(), shim.SpanId);
    }

    [Fact]
    public void GetBaggage()
    {
        var shim = GetSpanContextShim();
        var baggage = shim.GetBaggageItems();
        Assert.Empty(baggage);
    }

    internal static SpanContextShim GetSpanContextShim()
    {
        return new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));
    }
}
