// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Api.Tests;

public static class ActivityTraceFlagsTests
{
    [Fact]
    public static void ActivityTraceFlags_MembersAreKnown()
    {
        // Act
#if NET
        var actual = Enum.GetValues<ActivityTraceFlags>();
#else
        var actual = Enum.GetValues(typeof(ActivityTraceFlags)).OfType<ActivityTraceFlags>().ToArray();
#endif

        // If this test fails, new members have been added to the ActivityTraceFlags
        // enum. Review the code for any changes that are needed to accommodate the
        // new value(s) and then update this test to include them.
        Assert.Equal<ActivityTraceFlags>([ActivityTraceFlags.None, ActivityTraceFlags.Recorded, ActivityTraceFlags.RandomTraceId], actual);
    }
}
