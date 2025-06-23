// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace LinksAndParentBasedSamplerExample;

internal static class Program
{
    private static readonly ActivitySource MyActivitySource = new("LinksAndParentBasedSampler.Example");

    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
             .SetSampler(new LinksAndParentBasedSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.2))))
             .AddSource("LinksAndParentBasedSampler.Example")
             .AddConsoleExporter()
             .Build();

        for (var i = 0; i < 10; i++)
        {
            var links = GetActivityLinks(i);

            // Create a new activity that links to the activities in the list of activity links.
            using (var activity = MyActivitySource.StartActivity(ActivityKind.Internal, parentContext: default, tags: default, links: links))
            {
                activity?.SetTag("foo", "bar");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Generates a list of activity links. A linked activity is sampled with a probability of 0.1.
    /// </summary>
    /// <returns>A list of links.</returns>
    private static List<ActivityLink> GetActivityLinks(int seed)
    {
        var random = new Random(seed);
        var linkedActivitiesList = new List<ActivityLink>();

        for (var i = 0; i < 5; i++)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            int randomValue = random.Next(10);
#pragma warning restore CA5394 // Do not use insecure randomness
            var traceFlags = (randomValue == 0) ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), traceFlags);
            linkedActivitiesList.Add(new ActivityLink(context));
        }

        return linkedActivitiesList;
    }
}
