// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests.Stress;

public static class Program
{
    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<DemoStressTest>(args);
    }

    private sealed class DemoStressTest : StressTest<StressTestOptions>
    {
        public DemoStressTest(StressTestOptions options)
            : base(options)
        {
        }

        protected override void RunWorkItemInParallel()
        {
        }
    }
}
