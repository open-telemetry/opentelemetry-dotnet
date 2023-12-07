// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#pragma warning restore IDE0005

namespace OpenTelemetry.Instrumentation;

internal static class ActivityInstrumentationHelper
{
    internal static readonly Action<Activity, ActivityKind> SetKindProperty = CreateActivityKindSetter();
    internal static readonly Action<Activity, ActivitySource> SetActivitySourceProperty = CreateActivitySourceSetter();

    private static Action<Activity, ActivitySource> CreateActivitySourceSetter()
    {
        return (Action<Activity, ActivitySource>)typeof(Activity).GetProperty("Source")
            .SetMethod.CreateDelegate(typeof(Action<Activity, ActivitySource>));
    }

    private static Action<Activity, ActivityKind> CreateActivityKindSetter()
    {
        return (Action<Activity, ActivityKind>)typeof(Activity).GetProperty("Kind")
            .SetMethod.CreateDelegate(typeof(Action<Activity, ActivityKind>));
    }
}
