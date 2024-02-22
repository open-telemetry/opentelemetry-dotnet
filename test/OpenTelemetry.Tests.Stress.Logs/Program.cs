// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static ILogger logger;
    private static Payload payload = new Payload();

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<StressTestOptions>(args)
            .WithParsed(LaunchStressTest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        logger.Log(
            logLevel: LogLevel.Information,
            eventId: 2,
            state: payload,
            exception: null,
            formatter: (state, ex) => string.Empty);
    }

    protected static void WriteRunInformationToConsole(StressTestOptions options)
    {
    }

    private static void LaunchStressTest(StressTestOptions options)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new DummyProcessor());
            });
        });

        logger = loggerFactory.CreateLogger<Program>();

        RunStressTest(options);
    }
}
