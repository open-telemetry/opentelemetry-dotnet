// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Tests;

public class ActivityInstrumentationHelperTest
{
    [Theory]
    [InlineData("TestActivitySource", null)]
    [InlineData("TestActivitySource", "1.0.0")]
    public void SetActivitySource(string name, string version)
    {
        using var activity = new Activity("Test");
        using var activitySource = new ActivitySource(name, version);

        activity.Start();
        ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySource);
        Assert.Equal(activitySource.Name, activity.Source.Name);
        Assert.Equal(activitySource.Version, activity.Source.Version);
        activity.Stop();
    }

    [Theory]
    [InlineData(ActivityKind.Client)]
    [InlineData(ActivityKind.Consumer)]
    [InlineData(ActivityKind.Internal)]
    [InlineData(ActivityKind.Producer)]
    [InlineData(ActivityKind.Server)]
    public void SetActivityKind(ActivityKind activityKind)
    {
        using var activity = new Activity("Test");
        activity.Start();
        ActivityInstrumentationHelper.SetKindProperty(activity, activityKind);
        Assert.Equal(activityKind, activity.Kind);
    }
}
