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

namespace SDKBasedSpanLevelTailSamplingSample;

internal class Program
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
