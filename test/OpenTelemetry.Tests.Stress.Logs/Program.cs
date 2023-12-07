// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static ILogger logger;
    private static Payload payload = new Payload();

    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new DummyProcessor());
            });
        });

        logger = loggerFactory.CreateLogger<Program>();

        Stress(prometheusPort: 9464);
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
}