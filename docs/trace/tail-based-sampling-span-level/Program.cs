// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace SDKBasedSpanLevelTailSamplingSample;

internal class Program
{
    private static readonly ActivitySource MyActivitySource = new("SDK.TailSampling.POC");

    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new ParentBasedElseAlwaysRecordSampler())
            .AddSource("SDK.TailSampling.POC")
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new TailSamplingProcessor())
#pragma warning restore CA2000 // Dispose objects before losing scope
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
#pragma warning disable CA5394 // Do not use insecure randomness
                var randomValue = random.Next(5);
#pragma warning restore CA5394 // Do not use insecure randomness
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
