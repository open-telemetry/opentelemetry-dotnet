// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Examples.EnvironmentVariables;

internal static class Program
{
    private const string ActivitySourceName = "Examples.EnvironmentVariables";
    private const string ChildModeArgument = "--child";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly TextMapPropagator Propagator = new CompositeTextMapPropagator(
    [
        new TraceContextPropagator(),
        new BaggagePropagator(),
    ]);

    public static async Task<int> Main(string[] args)
    {
        using var listener = CreateActivityListener();

        return args.Contains(ChildModeArgument, StringComparer.Ordinal)
            ? RunAsChild()
            : await RunAsParentAsync();
    }

    private static async Task<int> RunAsParentAsync()
    {
        Baggage.ClearBaggage();
        Baggage.SetBaggage("tenant.id", "contoso");
        Baggage.SetBaggage("user.id", "alice");

        using var activity = ActivitySource.StartActivity("parent-process");
        if (activity == null)
        {
            await Console.Error.WriteLineAsync("Failed to create the parent activity.");
            return 1;
        }

        WriteProcessContext("Parent", activity, default, Baggage.Current);

        var startInfo = CreateChildStartInfo();

        CopyCurrentEnvironment(startInfo.Environment);

        var context = new PropagationContext(activity.Context, Baggage.Current);
        Propagator.Inject(context, startInfo.Environment, EnvironmentVariableCarrier.Set);

        Console.WriteLine("[Parent] Injected environment variables:");
        WritePropagationFields("Parent", startInfo.Environment);
        Console.WriteLine();

        using var child = Process.Start(startInfo);
        if (child == null)
        {
            await Console.Error.WriteLineAsync("Failed to start the child process.");
            return 1;
        }

        // See https://stackoverflow.com/a/16326426/1064169 and
        // https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput.
        using var outputTokenSource = new CancellationTokenSource();

#pragma warning disable CA2025
        var readOutput = ReadOutputAsync(child, outputTokenSource.Token);
#pragma warning restore CA2025

        await child.WaitForExitAsync();

        (string childStandardError, string childStandardOutput) = await readOutput;

        if (!string.IsNullOrEmpty(childStandardOutput))
        {
            Console.WriteLine();
            Console.Write(childStandardOutput);
        }

        if (!string.IsNullOrEmpty(childStandardError))
        {
            await Console.Error.WriteAsync(childStandardError);
        }

        Console.WriteLine();
        Console.WriteLine($"[Parent] Child process exited with code {child.ExitCode}.");

        return child.ExitCode;

        static async Task<(string Error, string Output)> ReadOutputAsync(
            Process process,
            CancellationToken cancellationToken)
        {
            var processErrors = ConsumeStreamAsync(process.StandardError, process.StartInfo.RedirectStandardError, cancellationToken);
            var processOutput = ConsumeStreamAsync(process.StandardOutput, process.StartInfo.RedirectStandardOutput, cancellationToken);

            await Task.WhenAll(processErrors, processOutput);

            string error = string.Empty;
            string output = string.Empty;

            if (processErrors.Status == TaskStatus.RanToCompletion)
            {
                error = (await processErrors).ToString();
            }

            if (processOutput.Status == TaskStatus.RanToCompletion)
            {
                output = (await processOutput).ToString();
            }

            return (error, output);
        }

        static Task<StringBuilder> ConsumeStreamAsync(
            StreamReader reader,
            bool isRedirected,
            CancellationToken cancellationToken)
        {
            return isRedirected ?
                Task.Run(() => ProcessStream(reader, cancellationToken), cancellationToken) :
                Task.FromResult(new StringBuilder(0));

            static async Task<StringBuilder> ProcessStream(
                StreamReader reader,
                CancellationToken cancellationToken)
            {
                var builder = new StringBuilder();

                try
                {
                    builder.Append(await reader.ReadToEndAsync(cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    // Ignore
                }

                return builder;
            }
        }
    }

    private static int RunAsChild()
    {
        var carrier = EnvironmentVariableCarrier.Capture();
        var parentContext = Propagator.Extract(default, carrier, EnvironmentVariableCarrier.Get);

        Baggage.Current = parentContext.Baggage;

        using var activity = ActivitySource.StartActivity(
            "child-process",
            ActivityKind.Internal,
            parentContext.ActivityContext);

        if (activity == null)
        {
            Console.Error.WriteLine("Failed to create the child activity.");
            return 1;
        }

        Console.WriteLine("  [Child] Captured propagated environment variables:");
        WritePropagationFields("Child", carrier);
        Console.WriteLine();

        WriteProcessContext("Child", activity, parentContext.ActivityContext, Baggage.Current);

        return 0;
    }

    private static ActivityListener CreateActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static ProcessStartInfo CreateChildStartInfo()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current process path is unavailable.");

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("The entry assembly path is unavailable.");

        var fileName = Path.GetFileNameWithoutExtension(processPath);

        var startInfo = new ProcessStartInfo(processPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        startInfo.ArgumentList.Add(ChildModeArgument);

        return startInfo;
    }

    private static void CopyCurrentEnvironment(IDictionary<string, string?> environment)
    {
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            environment[(string)variable.Key] = variable.Value?.ToString();
        }
    }

    private static void WriteProcessContext(
        string role,
        Activity activity,
        ActivityContext parentContext,
        Baggage baggage)
    {
        string indent = role is "Parent" ? string.Empty : "  ";

        Console.WriteLine($"{indent}[{role}] ProcessId: {Environment.ProcessId}");
        Console.WriteLine($"{indent}[{role}] TraceId: {activity.TraceId}");
        Console.WriteLine($"{indent}[{role}] SpanId: {activity.SpanId}");
        Console.WriteLine($"{indent}[{role}] ParentSpanId: {FormatSpanId(activity.ParentSpanId)}");

        if (parentContext != default)
        {
            Console.WriteLine($"{indent}[{role}] ExtractedParentTraceId: {parentContext.TraceId}");
            Console.WriteLine($"{indent}[{role}] ExtractedParentSpanId: {FormatSpanId(parentContext.SpanId)}");
        }

        Console.WriteLine($"{indent}[{role}] Baggage: {FormatBaggage(baggage)}");

        static string FormatSpanId(ActivitySpanId spanId)
        {
            return spanId == default ? "<none>" : spanId.ToString();
        }

        static string FormatBaggage(Baggage baggage)
        {
            if (baggage.Count == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder();

            foreach (var item in baggage.GetBaggage())
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(item.Key);
                builder.Append('=');
                builder.Append(item.Value);
            }

            return builder.ToString();
        }
    }

    private static void WritePropagationFields<T>(string role, T carrier)
        where T : IEnumerable<KeyValuePair<string, string?>>
    {
        if (Propagator.Fields is not { Count: > 0 } fields)
        {
            return;
        }

        string indent = role is "Parent" ? string.Empty : "  ";

        foreach (var field in fields)
        {
            var normalized = EnvironmentVariableCarrier.NormalizeKey(field);
            var values = EnvironmentVariableCarrier.Get(carrier, field);
            var value = values?.FirstOrDefault();

            Console.WriteLine($"{indent}[{role}]   {normalized}={value ?? "<not set>"}");
        }
    }
}
