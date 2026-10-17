// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
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

    internal static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProbeDeadline = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the <see cref="HttpClient"/> used to reach the collector on the host.
    /// </summary>
    /// <remarks>
    /// Shared client used to avoid timeouts while establishing connections in the simulator.
    /// </remarks>
    internal static HttpClient OtlpHttpClient { get; } = new() { Timeout = ExportTimeout };

    private static async Task Main(string[] args)
    {
        var resultsDirectory = Path.Combine(DocumentsDirectory(), ResultsDirectoryName);

        Directory.CreateDirectory(resultsDirectory);

        // Use short export defaults to avoid waiting too long for the test results to be exported
        Environment.SetEnvironmentVariable("OTEL_BSP_SCHEDULE_DELAY", "1000");
        Environment.SetEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "1000");
        Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");

        // The test platform replaces Console.Out while the tests run and only
        // reports the output it captures for tests that fail, so the real one is
        // held on to here to keep the diagnostics visible in the streamed output.
        using var diagnostics = new SelfDiagnosticsListener(Console.Out);

        await Console.Out.WriteLineAsync("Exporting OTLP to " + InstrumentationSource.OtlpEndpoint).ConfigureAwait(false);

        await WaitForCollectorAsync().ConfigureAwait(false);

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
                ]).ConfigureAwait(false);

            builder.AddMSTest(() => [typeof(TestRunner).Assembly]);
            builder.AddTrxReportProvider();
            builder.TestHost.AddDataConsumer(_ => consumer);

            // The test application is deliberately not disposed of here. The
            // process is terminated as soon as the results have been written,
            // and disposing it can block, which would stop the results from
            // being written at all.
            var app = await builder.BuildAsync().ConfigureAwait(false);

            exitCode = await app.RunAsync().ConfigureAwait(false);
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

        await File.WriteAllTextAsync(Path.Combine(resultsDirectory, SummaryFileName), summary).ConfigureAwait(false);

        if (failure is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(resultsDirectory, ErrorFileName), failure.ToString()).ConfigureAwait(false);
        }

        await Console.Out.WriteAsync(summary).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);

        // Terminate explicitly so that 'xcrun simctl launch --console-pty'
        // returns as soon as the run finishes instead of waiting on any threads
        // the test platform left running.
        Environment.Exit(failed == 0 && consumer.Passed > 0 ? 0 : 1);
    }

    /// <summary>
    /// Waits until the collector on the host answers a request from the simulator.
    /// </summary>
    /// <remarks>
    /// The first requests made after the app starts have been seen hanging until
    /// they time out, only for the endpoint to become reachable a few seconds
    /// later. An export lost that way cannot be recovered - the batch processors
    /// drop a batch whose export failed - so the wait happens here, before any
    /// test runs, rather than costing a test its telemetry. Nothing is asserted:
    /// if the collector never answers the tests report that far better than a
    /// failure from the entry point would.
    /// </remarks>
    private static async Task WaitForCollectorAsync()
    {
        // Any path the collector does not map answers 404, which proves it is
        // reachable without adding to the requests the host asserts against.
        var probe = new Uri(new Uri(InstrumentationSource.OtlpEndpoint), "ready");

        var startedAt = Stopwatch.GetTimestamp();
        var deadline = DateTime.UtcNow + ProbeDeadline;

        for (var attempt = 1; DateTime.UtcNow < deadline; attempt++)
        {
            try
            {
                using var timeout = new CancellationTokenSource(ProbeTimeout);
                using var response = await OtlpHttpClient.GetAsync(probe, timeout.Token).ConfigureAwait(false);

                await Console.Out.WriteLineAsync(
                    FormattableString.Invariant(
                        $"Collector answered on attempt {attempt} after {Stopwatch.GetElapsedTime(startedAt).TotalSeconds:N1}s."))
                    .ConfigureAwait(false);

                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await Console.Out.WriteLineAsync(
                    FormattableString.Invariant(
                        $"Collector did not answer on attempt {attempt} after {Stopwatch.GetElapsedTime(startedAt).TotalSeconds:N1}s: {ex.Message}"))
                    .ConfigureAwait(false);

                // A connection that is refused rather than left hanging comes back
                // immediately, so pause before trying again.
                await Task.Delay(ProbeInterval).ConfigureAwait(false);
            }
        }

        await Console.Out.WriteLineAsync(
            FormattableString.Invariant($"Collector was still unreachable after {ProbeDeadline}; running the tests anyway."))
            .ConfigureAwait(false);
    }

    private static string DocumentsDirectory()
        => NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User).FirstOrDefault()
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private sealed class SelfDiagnosticsListener : EventListener
    {
        // This is static because the base constructor can call
        // OnEventSourceCreated - and so start writing events - before
        // the fields of this class would have been assigned.
        private static TextWriter? output;

        internal SelfDiagnosticsListener(TextWriter writer)
        {
            output = TextWriter.Synchronized(writer);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.Ordinal))
            {
                this.EnableEvents(eventSource, EventLevel.Warning, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (string.Equals(eventData.EventName, "MetricInstrumentIgnored", StringComparison.Ordinal))
            {
                // Noise we do not care about for these tests
                return;
            }

            var payload = eventData.Payload is null
                ? string.Empty
                : string.Join(", ", eventData.Payload);

            output?.WriteLine($"{eventData.EventSource?.Name}: {eventData.EventName}: {payload}");
        }
    }

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
            var message = outcome + ": " + (id is not null ? $"{id.Namespace}.{id.TypeName}.{id.MethodName}" : node.DisplayName);

#if NET11_0_OR_GREATER
            await Console.Out.WriteLineAsync(message, cancellationToken).ConfigureAwait(false);
#else
            await Console.Out.WriteLineAsync(message).ConfigureAwait(false);
#endif
        }
    }
}
