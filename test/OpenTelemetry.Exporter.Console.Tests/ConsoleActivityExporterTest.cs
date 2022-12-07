// <copyright file="ConsoleActivityExporterTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleActivityExporterTest
{
    [Fact]
    public void VerifyConsoleActivityExporterDoesntFailWithoutActivityLinkTags()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

        var exportedItems = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .AddInMemoryExporter(exportedItems)
            .Build();

        ActivityContext context;
        using (var first = activitySource.StartActivity("first"))
        {
            context = first!.Context;
        }

        exportedItems.Clear();

        var links = new[] { new ActivityLink(context) };
        using (var secondActivity = activitySource.StartActivity(ActivityKind.Internal, links: links, name: "Second"))
        {
        }

        // Assert that an Activity was exported where ActivityLink.Tags == null.
        var activity = exportedItems[0];
        Assert.Equal("Second", activity.DisplayName);
        Assert.Null(activity.Links.First().Tags);

        // Test that the ConsoleExporter correctly handles an Activity without Tags.
        using var consoleExporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, consoleExporter.Export(new Batch<Activity>(new[] { activity }, 1)));
    }
}
