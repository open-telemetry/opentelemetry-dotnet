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

namespace LinksCreationWithNewRootActivitiesDemo;

internal class Program
{
    private static readonly ActivitySource MyActivitySource = new("LinksCreationWithNewRootActivities");

    public static async Task Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("LinksCreationWithNewRootActivities")
                .AddConsoleExporter()
                .Build();

        using (var activity = MyActivitySource.StartActivity("OrchestratingActivity"))
        {
            activity?.SetTag("foo", 1);
            await DoFanoutAsync();

            using (var nestedActivity = MyActivitySource.StartActivity("WrapUp"))
            {
                nestedActivity?.SetTag("foo", 1);
            }
        }
    }

    public static async Task DoFanoutAsync()
    {
        var previous = Activity.Current;
        const int NumConcurrentOperations = 10;

        var activityContext = Activity.Current!.Context;
        var links = new List<ActivityLink>
        {
            new ActivityLink(activityContext),
        };

        var tasks = new List<Task>();

        // Fanning out to N concurrent operations.
        // We create a new root activity for each operation and
        // link it to an outer activity that happens to be the current
        // activity.
        for (int i = 0; i < NumConcurrentOperations; i++)
        {
            int operationIndex = i;

            var task = Task.Run(() =>
            {
                // Reference: https://opentelemetry.io/docs/instrumentation/net/manual/#creating-new-root-activities
                // Since we want to create a new root activity for each of the fanned out operations,
                // this step helps us "de-parent" it from the current activity.
                // Note: At least as of Oct 2023, this is the only mechanism to create a new root
                // activity in the presence of an existing activity. This might change in the future
                // if/when issue https://github.com/open-telemetry/opentelemetry-dotnet/issues/984
                // is addressed.
                Activity.Current = null;

                // Reference: https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api#activity-creation-options
                // Reference: https://opentelemetry.io/docs/instrumentation/net/manual/#adding-links
                // We create a new root activity for each of the fanned out operations and link it to the outer activity.
                using var newRootActivityForFannedOutOperation = MyActivitySource.StartActivity(
                    ActivityKind.Internal,  // Set this to the appropriate ActivityKind depending on your scenario
                    name: $"FannedOutActivity {operationIndex + 1}",
                    links: links);

                // DO THE FANOUT WORK HERE...
            });

            tasks.Add(task);
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Reset to the previous activity now that we are done with the fanout
        // This will ensure that the rest of the code executes in the context of the original activity.
        Activity.Current = previous;
    }
}
