// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace.Tests;

public class SamplingParametersTests
{
    [Fact]
    public void Verify_Equals()
    {
        var parameters1 = CreateParameters(name: "a");
        Assert.True(parameters1.Equals(parameters1));
        Assert.True(parameters1.Equals((object)parameters1));

        var parameters2 = CreateParameters(name: "a");
        Assert.True(parameters1.Equals(parameters2));
        Assert.True(parameters1.Equals((object)parameters2));

        var differentName = CreateParameters(name: "b");
        Assert.False(parameters1.Equals(differentName));
        Assert.False(parameters1.Equals((object)differentName));

        var differentTraceId = CreateParameters(name: "a", traceId: ActivityTraceId.CreateRandom());
        Assert.False(parameters1.Equals(differentTraceId));

        var differentKind = CreateParameters(name: "a", kind: ActivityKind.Client);
        Assert.False(parameters1.Equals(differentKind));

        var differentParentContext = CreateParameters(name: "a", parentContext: new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));
        Assert.False(parameters1.Equals(differentParentContext));

        Assert.False(parameters1.Equals(Guid.Empty));
    }

    [Fact]
    public void Verify_Equals_Tags()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("key1", "value1"), new("key2", 2) };
        var tags2 = new List<KeyValuePair<string, object?>> { new("key1", "value1"), new("key2", 2) };

        var parameters1 = CreateParameters(tags: tags1);
        var parameters2 = CreateParameters(tags: tags2);

        // Tags are compared element-by-element.
        Assert.True(parameters1.Equals(parameters2));

        var differentTags = new List<KeyValuePair<string, object?>> { new("key1", "other") };
        var parameters3 = CreateParameters(tags: differentTags);
        Assert.False(parameters1.Equals(parameters3));
    }

    [Fact]
    public void Verify_Equals_Links()
    {
        var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var links1 = new List<ActivityLink> { new(context) };
        var links2 = new List<ActivityLink> { new(context) };

        var parameters1 = CreateParameters(links: links1);
        var parameters2 = CreateParameters(links: links2);

        // Links are compared element-by-element.
        Assert.True(parameters1.Equals(parameters2));

        var differentLinks = new List<ActivityLink> { new(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)) };
        var parameters3 = CreateParameters(links: differentLinks);
        Assert.False(parameters1.Equals(parameters3));
    }

    [Fact]
    public void Verify_Equals_NullAndEmptyTagsAreEqual()
    {
        var parameters1 = CreateParameters(tags: null);
        var parameters2 = CreateParameters(tags: []);

        Assert.True(parameters1.Equals(parameters2));
    }

    [Fact]
    public void Verify_Equals_DefaultInstances()
    {
        Assert.True(default(SamplingParameters).Equals(default(SamplingParameters)));
        Assert.True(default(SamplingParameters) == default(SamplingParameters));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var parameters1 = CreateParameters(name: "a");
        var parameters2 = CreateParameters(name: "a");
        var parameters3 = CreateParameters(name: "b");

        Assert.True(parameters1 == parameters2);
        Assert.False(parameters1 == parameters3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var parameters1 = CreateParameters(name: "a");
        var parameters2 = CreateParameters(name: "a");
        var parameters3 = CreateParameters(name: "b");

        Assert.False(parameters1 != parameters2);
        Assert.True(parameters1 != parameters3);
    }

    [Fact]
    public void Verify_GetHashCode()
    {
        var parameters1 = CreateParameters(name: "a");
        var parameters2 = CreateParameters(name: "a");
        var parameters3 = CreateParameters(name: "b");

        Assert.Equal(parameters1.GetHashCode(), parameters2.GetHashCode());
        Assert.NotEqual(parameters1.GetHashCode(), parameters3.GetHashCode());
    }

    private static SamplingParameters CreateParameters(
        string name = "test",
        ActivityTraceId? traceId = null,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext? parentContext = null,
        IEnumerable<KeyValuePair<string, object?>>? tags = null,
        IEnumerable<ActivityLink>? links = null) =>
        new(
            parentContext ?? new ActivityContext(
                ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
                ActivitySpanId.CreateFromString("b7ad6b7169203331"),
                ActivityTraceFlags.Recorded),
            traceId ?? ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
            name,
            kind,
            tags,
            links);
}
