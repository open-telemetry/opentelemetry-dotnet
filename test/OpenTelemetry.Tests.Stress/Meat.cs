// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    public static void Main()
    {
        RunStressTest(new()
        {
            Concurrency = 1,
            PrometheusInternalMetricsPort = 9464,
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
    }

    protected static void WriteRunInformationToConsole(StressTestOptions options)
    {
    }
}
