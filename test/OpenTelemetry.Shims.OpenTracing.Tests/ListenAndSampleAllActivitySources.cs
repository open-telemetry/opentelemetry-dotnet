// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[CollectionDefinition(nameof(ListenAndSampleAllActivitySources))]
public sealed class ListenAndSampleAllActivitySources : ICollectionFixture<ListenAndSampleAllActivitySources.Fixture>
{
    public sealed class Fixture : IDisposable
    {
        private readonly ActivityListener listener;

        public Fixture()
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
}
