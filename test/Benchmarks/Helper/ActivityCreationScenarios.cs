// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace Benchmarks.Helper;

internal static class ActivityCreationScenarios
{
    public static void CreateActivity(ActivitySource source)
    {
        using var activity = source.StartActivity("name");
        activity?.Stop();
    }

    public static void CreateActivityFromParentContext(ActivitySource source, ActivityContext parentCtx)
    {
        using var activity = source.StartActivity("name", ActivityKind.Internal, parentCtx);
        activity?.Stop();
    }

    public static void CreateActivityWithAttributes(ActivitySource source)
    {
        using var activity = source.StartActivity("name");
        activity?.SetTag("tag1", "string");
        activity?.SetTag("tag2", 1);
        activity?.SetTag("tag3", true);
        activity?.SetTag("tag4", "string-again");
        activity?.SetTag("tag5", "string-more");
        activity?.Stop();
    }

    public static void CreateActivityWithAddAttributes(ActivitySource source)
    {
        using var activity = source.StartActivity("name");
        activity?.AddTag("tag1", "string");
        activity?.AddTag("tag2", 1);
        activity?.AddTag("tag3", true);
        activity?.AddTag("tag4", "string-again");
        activity?.AddTag("tag5", "string-more");
        activity?.Stop();
    }
}
