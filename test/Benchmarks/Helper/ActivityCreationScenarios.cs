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

    public static void CreateActivityWithKind(ActivitySource source)
    {
        using var activity = source.StartActivity("name", ActivityKind.Client);
        activity?.Stop();
    }

    public static void CreateActivityFromParentContext(ActivitySource source, ActivityContext parentCtx)
    {
        using var activity = source.StartActivity("name", ActivityKind.Internal, parentCtx);
        activity?.Stop();
    }

    public static void CreateActivityFromParentId(ActivitySource source, string parentId)
    {
        using var activity = source.StartActivity("name", ActivityKind.Internal, parentId);
        activity?.Stop();
    }

    public static void CreateActivityWithAttributes(ActivitySource source)
    {
        using var activity = source.StartActivity("name");
        activity?.SetTag("tag1", "string");
        activity?.SetTag("tag2", 1);
        activity?.SetTag("tag3", true);
        activity?.Stop();
    }
}