// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace StratifiedSamplingByQueryTypeDemo;

internal sealed class Program
{
    private static readonly ActivitySource MyActivitySource = new("StratifiedSampling.POC");

    public static void Main(string[] args)
    {
        // We wrap the stratified sampler within a parentbased sampler.
        // This is to enable downstream participants (i.e., the non-root spans) to have
        // the same consistent sampling decision as the root span (that uses the stratified sampler).
        // Such downstream participants may not have access to the same attributes that were used to
        // make the stratified sampling decision at the root.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new ParentBasedSampler(new StratifiedSampler()))
            .AddSource("StratifiedSampling.POC")
            .AddConsoleExporter()
            .Build();

        var random = new Random(2357);
        var tagsList = new List<KeyValuePair<string, object?>>(1);

        // Generate some spans
        for (var i = 0; i < 20; i++)
        {
            // Simulate a mix of user-initiated (25%) and programmatic (75%) queries
            var randomValue = random.Next(4);
            switch (randomValue)
            {
                case 0:
                    tagsList.Add(new KeyValuePair<string, object?>("queryType", "userInitiated"));
                    break;
                default:
                    tagsList.Add(new KeyValuePair<string, object?>("queryType", "programmatic"));
                    break;
            }

            // Note that the queryType attribute here is present as part of the tags list when the activity is started.
            // We are using this attribute value to achieve stratified sampling.
            using (var activity = MyActivitySource.StartActivity(ActivityKind.Internal, parentContext: default, tags: tagsList))
            {
                activity?.SetTag("foo", "bar");
                using (var activity2 = MyActivitySource.StartActivity(ActivityKind.Internal, parentContext: default, tags: tagsList))
                {
                    activity2?.SetTag("foo", "child");
                }
            }

            tagsList.Clear();
        }
    }
}
