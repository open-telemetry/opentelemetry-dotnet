// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation;

internal static class ActivityInstrumentationHelper
{
    internal static readonly Action<Activity, ActivitySource> SetActivitySourceProperty = CreateActivitySourceSetter();

    private static Action<Activity, ActivitySource> CreateActivitySourceSetter()
    {
        return (Action<Activity, ActivitySource>)typeof(Activity).GetProperty("Source")
            .SetMethod.CreateDelegate(typeof(Action<Activity, ActivitySource>));
    }
}
