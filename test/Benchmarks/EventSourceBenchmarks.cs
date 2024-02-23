// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Benchmarks;

public class EventSourceBenchmarks
{
    [Benchmark]
    public void EventWithIdAllocation()
    {
        using var activity = new Activity("TestActivity");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        activity.Stop();

        OpenTelemetrySdkEventSource.Log.ActivityStart(activity.OperationName, activity.Id);
    }

    [Benchmark]
    public void EventWithCheck()
    {
        using var activity = new Activity("TestActivity");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        activity.Stop();

        OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);
    }
}
