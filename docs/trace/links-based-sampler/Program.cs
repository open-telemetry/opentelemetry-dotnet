// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace LinksAndParentBasedSamplerExample;

internal class Program
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
    private static IEnumerable<ActivityLink> GetActivityLinks(int seed)
    {
        var random = new Random(seed);
        var linkedActivitiesList = new List<ActivityLink>();

        for (var i = 0; i < 5; i++)
        {
            int randomValue = random.Next(10);
            var traceFlags = (randomValue == 0) ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), traceFlags);
            linkedActivitiesList.Add(new ActivityLink(context));
        }

        return linkedActivitiesList;
    }
}
