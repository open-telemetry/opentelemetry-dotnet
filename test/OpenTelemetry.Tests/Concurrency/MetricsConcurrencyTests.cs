// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Metrics.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Tests.Concurrency;

public class MetricsConcurrencyTests
{
    private readonly ITestOutputHelper output;
    private readonly AggregatorTests aggregatorTests;

    public MetricsConcurrencyTests(ITestOutputHelper output)
    {
        this.output = output;
        this.aggregatorTests = new AggregatorTests();
    }

    [SkipUnlessEnvVarFoundFact("OTEL_RUN_COYOTE_TESTS")]
    [Trait("CategoryName", "CoyoteConcurrencyTests")]
    public void MultithreadedLongHistogramTestConcurrencyTest()
    {
        var config = Configuration.Create()
            .WithTestingIterations(100)
            .WithMemoryAccessRaceCheckingEnabled(true);

        using var test = TestingEngine.Create(config, this.aggregatorTests.MultiThreadedHistogramUpdateAndSnapShotTest);

        test.Run();

        this.output.WriteLine(test.GetReport());
        this.output.WriteLine($"Bugs, if any: {string.Join("\n", test.TestReport.BugReports)}");

        var dir = Directory.GetCurrentDirectory();
        if (test.TryEmitReports(dir, $"{nameof(this.MultithreadedLongHistogramTestConcurrencyTest)}_CoyoteOutput", out var reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                this.output.WriteLine($"Execution Report: {reportPath}");
            }
        }

        if (test.TryEmitCoverageReports(dir, $"{nameof(this.MultithreadedLongHistogramTestConcurrencyTest)}_CoyoteOutput", out reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                this.output.WriteLine($"Coverage report: {reportPath}");
            }
        }

        Assert.Equal(0, test.TestReport.NumOfFoundBugs);
    }
}
