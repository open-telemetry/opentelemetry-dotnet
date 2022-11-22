// <copyright file="ConsoleExporterTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using OpenTelemetry.Tests.Shared;
    using OpenTelemetry.Trace;
    using Xunit;

    [VerifyNoEventSourceErrorsLoggedTest("OpenTelemetry-Sdk")]
    public class ConsoleExporterTest
    {
        /// <summary>
        /// Test case for https://github.com/open-telemetry/opentelemetry-dotnet/issues/3863.
        /// </summary>
        [Fact]
        public void VerifyConsoleActivityExporterDoesntFailWithoutActivityLinkTags()
        {
            var exportedItems = new List<Activity>();

            using var source = new ActivitySource("Testing");

            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Testing")
                .AddConsoleExporter()
                .AddInMemoryExporter(exportedItems)
                .Build();

            ActivityContext context;
            using (var first = source.StartActivity("first"))
            {
                context = first!.Context;
            }

            var links = new[] { new ActivityLink(context) };
            using (var secondActivity = source.StartActivity(ActivityKind.Internal, links: links, name: "Second"))
            {
            }

            // Wait for the Activity to dispose and be exported.
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return exportedItems.Any(x => x.DisplayName == "Second");
                },
                TimeSpan.FromSeconds(1)));

            // Assert that an Activity was exported where ActivityLink.Tags == null.
            var activity = exportedItems.First(x => x.DisplayName == "Second");
            Assert.Null(activity.Links.First().Tags);
        }
    }
}
