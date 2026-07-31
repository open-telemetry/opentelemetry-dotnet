// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace OpenTelemetry.Maui.TestApp;

/// <summary>
/// Runs the on-device tests with
/// <see href="https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro">Microsoft.Testing.Platform</see>
/// and reports the outcome back to <c>adb shell am instrument</c>, which the host
/// <c>OpenTelemetry.Maui.Tests</c> orchestrator drives the run with.
/// </summary>
[Instrumentation(Name = "io.opentelemetry.dotnet.maui.TestInstrumentation")]
public class TestInstrumentation : Instrumentation
{
    protected TestInstrumentation(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        this.Start();
    }

    public override async void OnStart()
    {
        base.OnStart();

        var consumer = new ResultConsumer(this);
        using var bundle = new Bundle();

        try
        {
            // Qualified because MAUI's implicit usings make 'Application' ambiguous
            // with Microsoft.Maui.Controls.Application.
            var writablePath = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? Path.GetTempPath();
            var resultsPath = Path.Combine(writablePath, "TestResults");

            var builder = await TestApplication.CreateBuilderAsync(
                [
                    "--results-directory", resultsPath,
                    "--report-trx"
                ]);

            builder.AddMSTest(() => [this.GetType().Assembly]);
            builder.AddTrxReportProvider();
            builder.TestHost.AddDataConsumer(_ => consumer);

            using ITestApplication app = await builder.BuildAsync();
            await app.RunAsync();

            bundle.PutInt("passed", consumer.Passed);
            bundle.PutInt("failed", consumer.Failed);
            bundle.PutInt("skipped", consumer.Skipped);
            bundle.PutString("resultsPath", consumer.TrxReportPath);

            this.Finish(Result.Ok, bundle);
        }
        catch (Exception ex)
        {
            bundle.PutString("error", ex.ToString());
            this.Finish(Result.Canceled, bundle);
        }
    }

    private sealed class ResultConsumer(Instrumentation instrumentation) : IDataConsumer
    {
        private int passed;
        private int failed;
        private int skipped;

        public int Passed => this.passed;

        public int Failed => this.failed;

        public int Skipped => this.skipped;

        public string? TrxReportPath { get; private set; }

        public string Uid => nameof(ResultConsumer);

        public string DisplayName => nameof(ResultConsumer);

        public string Description => string.Empty;

        public string Version => "1.0";

        public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact)];

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (value is SessionFileArtifact artifact)
            {
                this.TrxReportPath = artifact.FileInfo.FullName;
            }
            else if (value is TestNodeUpdateMessage { TestNode: var node })
            {
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
                    return Task.CompletedTask;
                }

                _ = outcome switch
                {
                    "passed" => Interlocked.Increment(ref this.passed),
                    "failed" => Interlocked.Increment(ref this.failed),
                    _ => Interlocked.Increment(ref this.skipped),
                };

                var id = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
                using var b = new Bundle();

                b.PutString("test", id is not null ? $"{id.Namespace}.{id.TypeName}.{id.MethodName}" : node.DisplayName);
                b.PutString("outcome", outcome);

                instrumentation.SendStatus(0, b);
            }

            return Task.CompletedTask;
        }
    }
}
