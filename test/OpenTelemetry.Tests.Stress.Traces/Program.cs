// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommandLine;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("OpenTelemetry.Tests.Stress");

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<StressTestOptions>(args)
            .WithParsed(LaunchStressTest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        using (var activity = ActivitySource.StartActivity("test"))
        {
            activity?.SetTag("foo", "value");
        }
    }

    protected static void WriteRunInformationToConsole(StressTestOptions options)
    {
    }

    private static void LaunchStressTest(StressTestOptions options)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySource.Name)
            .Build();

        RunStressTest(options);
    }
}
