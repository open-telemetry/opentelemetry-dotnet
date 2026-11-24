// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck.Fluent;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

public sealed class GeneratorsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ActivityArbitraryGeneratesAtRequestedSize(int size)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, Generators.TestActivitySource),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = Assert.Single(Generators.ActivityArbitrary().Generator.Sample(numberOfSamples: 1, size: size));

        Assert.Same(Generators.TestActivitySource, activity.Source);
        Assert.Equal(size, activity.TagObjects.Count());
        Assert.Equal(size / 10, activity.Events.Count());
        Assert.Equal(size / 10, activity.Links.Count());
    }

    [Fact]
    public void ResourceArbitraryGeneratesAtSizeZero()
    {
        var resource = Assert.Single(Generators.ResourceArbitrary().Generator.Sample(numberOfSamples: 1, size: 0));

        Assert.Empty(resource.Attributes);
    }

    [Fact]
    public void LogRecordArbitraryGeneratesAtSizeZero()
    {
        var record = Assert.Single(Generators.LogRecordArbitrary().Generator.Sample(numberOfSamples: 1, size: 0));
        try
        {
            Assert.NotNull(record.Severity);
            Assert.Null(record.Attributes);
        }
        finally
        {
            LogRecordSharedPool.Current.Return(record);
        }
    }
}
