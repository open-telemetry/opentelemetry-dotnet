// <copyright file="MetricsConcurrencyTests.cs" company="OpenTelemetry Authors">
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

using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Metrics.Tests;
using Xunit;

namespace OpenTelemetry.Tests.Concurrency;

public class MetricsConcurrencyTests : AggregatorTestsBase
{

    public MetricsConcurrencyTests()
        : base(false)
    {
    }

    [Fact]
    public void MultithreadedLongHistogramTest_Coyote()
    {
        var config = Configuration.Create()
            .WithTestingIterations(100)
            .WithMemoryAccessRaceCheckingEnabled(true);

        var test = TestingEngine.Create(config, this.MultiThreadedHistogramUpdateAndSnapShotTest);

        test.Run();
        Console.WriteLine(test.GetReport());
        Console.WriteLine($"Bugs, if any: {string.Join("\n", test.TestReport.BugReports)}");

        var dir = Directory.GetCurrentDirectory();

        if (test.TryEmitReports(dir, "MultithreadedLongHistogramTest_Coyote", out IEnumerable<string> reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                Console.WriteLine($"Execution Report: {reportPath}");
            }
        }

        if (test.TryEmitCoverageReports(dir, "MultithreadedLongHistogramTest_Coyote", out reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                Console.WriteLine($"Coverage report: {reportPath}");
            }
        }

        Assert.Equal(0, test.TestReport.NumOfFoundBugs);
    }
}
