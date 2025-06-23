// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;

/// <summary>
/// A custom processor for filtering <see cref="Activity"/> instances.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class MyFilteringProcessor : BaseProcessor<Activity>
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly Func<Activity, bool> filter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyFilteringProcessor"/>
    /// class.
    /// </summary>
    /// <param name="filter">Function used to test if an <see cref="Activity"/>
    /// should be recorded or dropped. Return <see langword="true"/> to record
    /// or <see langword="false"/> to drop.</param>
    public MyFilteringProcessor(Func<Activity, bool> filter)
    {
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public override void OnEnd(Activity activity)
    {
        // Bypass export if the Filter returns false.
        if (!this.filter(activity))
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
