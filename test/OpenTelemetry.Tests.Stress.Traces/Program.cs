// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Stress;

public static class Program
{
    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<TracesStressTest>(args);
    }

    private sealed class TracesStressTest : StressTests<StressTestOptions>
    {
        private static readonly ActivitySource ActivitySource = new("OpenTelemetry.Tests.Stress");
        private readonly TracerProvider tracerProvider;

        public TracesStressTest(StressTestOptions options)
            : base(options)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySource.Name)
                .Build();
        }

        public override void Dispose()
        {
            this.tracerProvider.Dispose();
            base.Dispose();
        }

        protected override void RunWorkItemInParallel()
        {
            using var activity = ActivitySource.StartActivity("test");

            activity?.SetTag("foo", "value");
        }
    }
}
