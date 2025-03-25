// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public sealed class ListenAndSampleAllActivitySourcesFixture : IDisposable
#pragma warning restore CA1515 // Consider making public types internal
{
    private readonly ActivityListener listener;

    public ListenAndSampleAllActivitySourcesFixture()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        this.listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.listener);
    }

    public void Dispose()
    {
        this.listener.Dispose();
    }
}
