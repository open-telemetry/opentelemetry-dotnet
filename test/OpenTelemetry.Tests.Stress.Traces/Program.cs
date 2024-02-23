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

    private sealed class TracesStressTest : StressTest<StressTestOptions>
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

        protected override void RunWorkItemInParallel()
        {
            using var activity = ActivitySource.StartActivity("test");

            activity?.SetTag("foo", "value");
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.tracerProvider.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
