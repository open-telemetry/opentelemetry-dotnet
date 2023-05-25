using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Coyote;
using Microsoft.Coyote.Logging;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Metrics.Tests;
using Xunit;

namespace OpenTelemetry.Tests.Metrics
{
    public class CurrencyTests
    {
        [Fact]
        public void MultithreadedLongHistogramTest_Coyote()
        {
            var config = Configuration.Create()
                .WithTestingIterations(100)
                .WithMemoryAccessRaceCheckingEnabled(true);

            // .WithVerbosityEnabled(VerbosityLevel.Debug)
            // .WithConsoleLoggingEnabled();
            // .WithPartiallyControlledConcurrencyAllowed(false);

            var test = TestingEngine.Create(config, AggregatorTest.MultiThreadedHistogramUpdateAndSnapShotTest);

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
}
