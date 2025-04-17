// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests.Stress;

internal static class Program
{
    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<DemoStressTest>(args);
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class DemoStressTest : StressTests<StressTestOptions>
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
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
