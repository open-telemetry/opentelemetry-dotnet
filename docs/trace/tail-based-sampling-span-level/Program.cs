// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace SDKBasedSpanLevelTailSamplingSample;

internal static class Program
{
    private static readonly ActivitySource MyActivitySource = new("SDK.TailSampling.POC");

    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new ParentBasedElseAlwaysRecordSampler())
            .AddSource("SDK.TailSampling.POC")
            .AddProcessor(new TailSamplingProcessor())
            .AddConsoleExporter()
            .Build();

        var random = new Random(2357);

        // Generate some spans
        for (var i = 0; i < 50; i++)
        {
            using (var activity = MyActivitySource.StartActivity("SayHello"))
            {
                activity?.SetTag("foo", "bar");

                // Simulate a mix of failed and successful spans
                var randomValue = random.Next(5);
                switch (randomValue)
                {
                    case 0:
                        activity?.SetStatus(ActivityStatusCode.Error);
                        break;
                    default:
                        activity?.SetStatus(ActivityStatusCode.Ok);
                        break;
                }
            }
        }
    }
}
