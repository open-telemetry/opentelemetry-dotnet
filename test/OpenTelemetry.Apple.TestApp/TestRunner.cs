// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace OpenTelemetry.Apple.TestApp;

/// <summary>
/// The entry point of the app. The app has no user interface: it runs the
/// on-device tests with <see href="https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro">Microsoft.Testing.Platform</see>
/// and writes the outcome to its own <c>Documents</c> directory, which the host
/// <c>OpenTelemetry.Apple.Tests</c> orchestrator reads back out of the simulator.
/// </summary>
internal static class TestRunner
{
    internal const string ResultsDirectoryName = "TestResults";
    internal const string SummaryFileName = "summary.txt";
    internal const string ErrorFileName = "error.txt";

    private static async Task Main(string[] args)
    {
        var resultsDirectory = Path.Combine(DocumentsDirectory(), ResultsDirectoryName);

        Directory.CreateDirectory(resultsDirectory);

        var consumer = new ResultConsumer();
        var exitCode = -1;
        Exception? failure = null;

        try
        {
            var builder = await TestApplication.CreateBuilderAsync(
                [
                    .. args,
                    "--results-directory", resultsDirectory,
                    "--report-trx"
                ]);

            builder.AddMSTest(() => [typeof(TestRunner).Assembly]);
            builder.AddTrxReportProvider();
            builder.TestHost.AddDataConsumer(_ => consumer);

            using var app = await builder.BuildAsync();

            exitCode = await app.RunAsync();
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // 'xcrun simctl launch' does not surface the app's exit code, so the
        // outcome of the run is written to a file that the host test project
        // reads back from the app's data container in the simulator.
        var failed = failure is null ? consumer.Failed : Math.Max(consumer.Failed, 1);

        string[] lines =
        [
            "passed=" + consumer.Passed.ToString(CultureInfo.InvariantCulture),
            "failed=" + failed.ToString(CultureInfo.InvariantCulture),
            "skipped=" + consumer.Skipped.ToString(CultureInfo.InvariantCulture),
            "exitCode=" + exitCode.ToString(CultureInfo.InvariantCulture),
        ];

        var summary = string.Join(Environment.NewLine, lines) + Environment.NewLine;

        await File.WriteAllTextAsync(Path.Combine(resultsDirectory, SummaryFileName), summary);

        if (failure is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(resultsDirectory, ErrorFileName), failure.ToString());
        }

        await Console.Out.WriteAsync(summary);
        await Console.Out.FlushAsync();

        // Terminate explicitly so that 'xcrun simctl launch --console-pty'
        // returns as soon as the run finishes instead of waiting on any threads
        // the test platform left running.
        Environment.Exit(failed == 0 && consumer.Passed > 0 ? 0 : 1);
    }

    private static string DocumentsDirectory()
        => NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User).FirstOrDefault()
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private sealed class ResultConsumer : IDataConsumer
    {
        private int passed;
        private int failed;
        private int skipped;

        public int Passed => this.passed;

        public int Failed => this.failed;

        public int Skipped => this.skipped;

        public string Uid => nameof(ResultConsumer);

        public string DisplayName => nameof(ResultConsumer);

        public string Description => string.Empty;

        public string Version => "1.0";

        public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (value is not TestNodeUpdateMessage { TestNode: var node })
            {
                return;
            }

            var state = node.Properties.SingleOrDefault<TestNodeStateProperty>();

            string? outcome = state switch
            {
                PassedTestNodeStateProperty => "passed",
                FailedTestNodeStateProperty or ErrorTestNodeStateProperty
                    or TimeoutTestNodeStateProperty => "failed",
                SkippedTestNodeStateProperty => "skipped",
                _ => null,
            };

            if (outcome is null)
            {
                return;
            }

            _ = outcome switch
            {
                "passed" => Interlocked.Increment(ref this.passed),
                "failed" => Interlocked.Increment(ref this.failed),
                _ => Interlocked.Increment(ref this.skipped),
            };

            var id = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();

            // Echoed to stdout so the progress of the run is visible in the
            // output streamed by 'xcrun simctl launch --console-pty'.
            await Console.Out.WriteLineAsync(
                outcome + ": " + (id is not null ? $"{id.Namespace}.{id.TypeName}.{id.MethodName}" : node.DisplayName));
        }
    }
}
