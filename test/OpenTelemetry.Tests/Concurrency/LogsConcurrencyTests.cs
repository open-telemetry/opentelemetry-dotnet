// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Logs.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Tests.Concurrency;

public class LogsConcurrencyTests
{
    private readonly ITestOutputHelper output;
    private readonly LogRecordSharedPoolTests logRecordSharedPoolTests;

    public LogsConcurrencyTests(ITestOutputHelper output)
    {
        this.output = output;
        this.logRecordSharedPoolTests = new LogRecordSharedPoolTests();
    }

    [SkipUnlessEnvVarFoundFact("OTEL_RUN_COYOTE_TESTS")]
    [Trait("CategoryName", "CoyoteConcurrencyTests")]
    public void LogPoolExportTestCoyote()
    {
        var config = Configuration.Create()
            .WithTestingIterations(100)
            .WithMemoryAccessRaceCheckingEnabled(true);

        var test = TestingEngine.Create(config, this.logRecordSharedPoolTests.ExportTest);

        test.Run();

        this.output.WriteLine(test.GetReport());
        this.output.WriteLine($"Bugs, if any: {string.Join("\n", test.TestReport.BugReports)}");

        var dir = Directory.GetCurrentDirectory();
        if (test.TryEmitReports(dir, $"{nameof(this.LogPoolExportTestCoyote)}_CoyoteOutput", out var reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                this.output.WriteLine($"Execution Report: {reportPath}");
            }
        }

        if (test.TryEmitCoverageReports(dir, $"{nameof(this.LogPoolExportTestCoyote)}_CoyoteOutput", out reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                this.output.WriteLine($"Coverage report: {reportPath}");
            }
        }

        Assert.Equal(0, test.TestReport.NumOfFoundBugs);
    }
}
