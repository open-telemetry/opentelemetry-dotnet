// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Benchmarks;

public class EventSourceBenchmarks
{
    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void EventWithIdAllocation()
#pragma warning restore CA1822 // Mark members as static
    {
        using var activity = new Activity("TestActivity");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        activity.Stop();

        OpenTelemetrySdkEventSource.Log.ActivityStarted(activity.OperationName, activity.Id!);
    }

    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void EventWithCheck()
#pragma warning restore CA1822 // Mark members as static
    {
        using var activity = new Activity("TestActivity");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        activity.Stop();

        OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);
    }
}
