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
}
