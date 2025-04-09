// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests;

public class TracestateUtilsTests
{
    [Fact]
    public void NullTracestate()
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        Assert.False(TraceStateUtils.AppendTraceState(null!, tracestateEntries));
        Assert.Empty(tracestateEntries);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void EmptyTracestate(string tracestate)
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        Assert.False(TraceStateUtils.AppendTraceState(tracestate, tracestateEntries));
        Assert.Empty(tracestateEntries);
    }

    [Theory]
    [InlineData("k=")]
    [InlineData("=v")]
    [InlineData("kv")]
    [InlineData("k =v")]
    [InlineData("k\t=v")]
    [InlineData("k=v,k=v")]
    [InlineData("k1=v1,,,k2=v2")]
    [InlineData("k=morethan256......................................................................................................................................................................................................................................................")]
    [InlineData("v=morethan256......................................................................................................................................................................................................................................................")]
    public void InvalidTracestate(string tracestate)
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        Assert.False(TraceStateUtils.AppendTraceState(tracestate, tracestateEntries));
        Assert.Empty(tracestateEntries);
    }

    [Fact]
    public void MaxEntries()
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        var tracestate =
            "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v";
        Assert.True(TraceStateUtils.AppendTraceState(tracestate, tracestateEntries));
        Assert.Equal(32, tracestateEntries.Count);
        Assert.Equal(
            "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v",
            TraceStateUtils.GetString(tracestateEntries));
    }

    [Fact]
    public void TooManyEntries()
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        var tracestate =
            "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v,k32=v";
        Assert.False(TraceStateUtils.AppendTraceState(tracestate, tracestateEntries));
        Assert.Empty(tracestateEntries);
    }

    [Theory]
    [InlineData("k=v", "k", "v")]
    [InlineData(" k=v ", "k", "v")]
    [InlineData("\tk=v", "k", "v")]
    [InlineData(" k= v ", "k", "v")]
    [InlineData(",k=v,", "k", "v")]
    [InlineData(", k= v, ", "k", "v")]
    [InlineData("k=\tv", "k", "v")]
    [InlineData("k=v\t", "k", "v")]
    [InlineData("1k=v", "1k", "v")]
    public void ValidPair(string pair, string expectedKey, string expectedValue)
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        Assert.True(TraceStateUtils.AppendTraceState(pair, tracestateEntries));
        Assert.Single(tracestateEntries);
        Assert.Equal(new KeyValuePair<string, string>(expectedKey, expectedValue), tracestateEntries.Single());
        Assert.Equal($"{expectedKey}={expectedValue}", TraceStateUtils.GetString(tracestateEntries));
    }

    [Theory]
    [InlineData("k1=v1,k2=v2")]
    [InlineData(" k1=v1 , k2=v2")]
    [InlineData(" ,k1=v1,k2=v2")]
    [InlineData("k1=v1,k2=v2, ")]
    public void ValidPairs(string tracestate)
    {
        var tracestateEntries = new List<KeyValuePair<string, string>>();
        Assert.True(TraceStateUtils.AppendTraceState(tracestate, tracestateEntries));
        Assert.Equal(2, tracestateEntries.Count);
        Assert.Contains(new KeyValuePair<string, string>("k1", "v1"), tracestateEntries);
        Assert.Contains(new KeyValuePair<string, string>("k2", "v2"), tracestateEntries);

        Assert.Equal("k1=v1,k2=v2", TraceStateUtils.GetString(tracestateEntries));
    }
}
