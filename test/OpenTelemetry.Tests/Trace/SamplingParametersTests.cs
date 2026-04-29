// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SamplingParametersTests
{
    [Fact]
    public void Verify_Equals_SameValues()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var tags = new List<KeyValuePair<string, object?>> { new("key", "value") };
        var links = new List<ActivityLink>();

        var params1 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal, tags, links);
        var params2 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal, tags, links);

        Assert.True(params1.Equals(params2));
        Assert.True(params1.Equals((object)params2));
    }

    [Fact]
    public void Verify_Equals_DifferentName()
    {
        var traceId = ActivityTraceId.CreateRandom();

        var params1 = new SamplingParameters(default, traceId, "op-a", ActivityKind.Internal);
        var params2 = new SamplingParameters(default, traceId, "op-b", ActivityKind.Internal);

        Assert.False(params1.Equals(params2));
    }

    [Fact]
    public void Verify_Equals_DifferentKind()
    {
        var traceId = ActivityTraceId.CreateRandom();

        var params1 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);
        var params2 = new SamplingParameters(default, traceId, "op", ActivityKind.Server);

        Assert.False(params1.Equals(params2));
    }

    [Fact]
    public void Verify_Equals_DifferentTagsReference()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var tags1 = new List<KeyValuePair<string, object?>> { new("key", "value") };
        var tags2 = new List<KeyValuePair<string, object?>> { new("key", "value") };

        var params1 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal, tags1);
        var params2 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal, tags2);

        Assert.False(params1.Equals(params2));
    }

    [Fact]
    public void Verify_Equals_WrongType()
    {
        var params1 = new SamplingParameters(default, ActivityTraceId.CreateRandom(), "op", ActivityKind.Internal);
        Assert.False(params1.Equals("not a SamplingParameters"));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var params1 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);
        var params2 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);
        var params3 = new SamplingParameters(default, traceId, "op", ActivityKind.Server);

        Assert.True(params1 == params2);
        Assert.False(params1 == params3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var params1 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);
        var params2 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);
        var params3 = new SamplingParameters(default, traceId, "op", ActivityKind.Server);

        Assert.False(params1 != params2);
        Assert.True(params1 != params3);
    }

    [Fact]
    public void Verify_GetHashCode_ConsistentForEqualInstances()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var params1 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);
        var params2 = new SamplingParameters(default, traceId, "op", ActivityKind.Internal);

        Assert.Equal(params1.GetHashCode(), params2.GetHashCode());
    }
}
