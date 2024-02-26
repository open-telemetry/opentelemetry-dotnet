// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace OpenTelemetry.Tests.Stress;

public static class StressTestFactory
{
    public static int RunSynchronously<TStressTest>(string[] commandLineArguments)
        where TStressTest : StressTest<StressTestOptions>
    {
        return RunSynchronously<TStressTest, StressTestOptions>(commandLineArguments);
    }

    public static int RunSynchronously<TStressTest, TStressTestOptions>(string[] commandLineArguments)
        where TStressTest : StressTest<TStressTestOptions>
        where TStressTestOptions : StressTestOptions
    {
        return Parser.Default.ParseArguments<TStressTestOptions>(commandLineArguments)
            .MapResult(
                CreateStressTestAndRunSynchronously,
                _ => 1);

        static int CreateStressTestAndRunSynchronously(TStressTestOptions options)
        {
            using var stressTest = (TStressTest)Activator.CreateInstance(typeof(TStressTest), options)!;

            stressTest.RunSynchronously();

            return 0;
        }
    }
}
