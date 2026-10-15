// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

[Collection(EnvVarsCollectionDefinition.Name)]
public class SelfDiagnosticsPreambleTests
{
    private const string UnclassifiedVar = "OTEL_ZZ_PREAMBLE_TEST_UNCLASSIFIED";
    private const string UnclassifiedValue = "s3cr3t-payload";
    private const string SafeVar = "OTEL_TRACES_SAMPLER";
    private const string SafeValue = "parentbased_always_on";

    [Fact]
    public void Build_DescribesTheSdkAndProcess()
    {
        var preamble = Build(EnvironmentVariableLogMode.None);

        Assert.Contains("=== OpenTelemetry .NET SDK self-diagnostics ===", preamble, StringComparison.Ordinal);
        Assert.Contains("=== end preamble ===", preamble, StringComparison.Ordinal);
        Assert.Contains("SDK version", preamble, StringComparison.Ordinal);
        Assert.Contains("Process ID", preamble, StringComparison.Ordinal);
        Assert.Contains("Machine name", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void NoneMode_OmitsAllEnvironmentVariableSections()
    {
        using var scope = EnvironmentVariableScope.Create(SafeVar, SafeValue);

        var preamble = Build(EnvironmentVariableLogMode.None);

        Assert.DoesNotContain("Environment Variables", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("Runtime environment variables:", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain(SafeVar, preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesMode_ListsNamesWithoutAnyValues()
    {
        using var scope = EnvironmentVariableScope.Create(
            (SafeVar, SafeValue),
            (UnclassifiedVar, UnclassifiedValue));

        var preamble = Build(EnvironmentVariableLogMode.Names);

        Assert.Contains(SafeVar, preamble, StringComparison.Ordinal);
        Assert.Contains(UnclassifiedVar, preamble, StringComparison.Ordinal);
        Assert.DoesNotContain(SafeValue, preamble, StringComparison.Ordinal);
        Assert.DoesNotContain(UnclassifiedValue, preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownSafeValuesMode_ShowsSafeValuesAndRedactsTheRest()
    {
        using var scope = EnvironmentVariableScope.Create(
            (SafeVar, SafeValue),
            (UnclassifiedVar, UnclassifiedValue));

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains($"{SafeVar} = {SafeValue}", preamble, StringComparison.Ordinal);

        Assert.Contains(UnclassifiedVar, preamble, StringComparison.Ordinal);
        Assert.DoesNotContain(UnclassifiedValue, preamble, StringComparison.Ordinal);
        Assert.Contains(
            $"{UnclassifiedVar} = {SelfDiagnosticsEnvironmentVariablePolicy.RedactedValue}",
            preamble,
            StringComparison.Ordinal);
    }

    [Fact]
    public void KnownSafeValuesMode_RedactsCredentialCarryingVariables()
    {
        using var scope = EnvironmentVariableScope.Create(
            "OTEL_EXPORTER_OTLP_HEADERS",
            "Authorization=Bearer super-secret-token");

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains("OTEL_EXPORTER_OTLP_HEADERS", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-token", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownSafeValuesMode_ReducesEndpointsToTheirAuthority()
    {
        using var scope = EnvironmentVariableScope.Create(
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "https://user:pw@collector.example.com:4317/v1/traces?token=abc");

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains("https://collector.example.com:4317", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("token=abc", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pw", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void AllValuesMode_DisclosesEverythingVerbatim()
    {
        using var scope = EnvironmentVariableScope.Create(
            (UnclassifiedVar, UnclassifiedValue),
            ("OTEL_EXPORTER_OTLP_HEADERS", "Authorization=Bearer opt-in-token"));

        var preamble = Build(EnvironmentVariableLogMode.AllValues);

        Assert.Contains($"{UnclassifiedVar} = {UnclassifiedValue}", preamble, StringComparison.Ordinal);
        Assert.Contains("opt-in-token", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void MisspelledVariableName_IsVisible()
    {
        using var scope = EnvironmentVariableScope.Create(
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "http://collector:4317");

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void NonOtelVariables_AreNeverIncluded()
    {
        using var scope = EnvironmentVariableScope.Create(
            "ZZ_PREAMBLE_TEST_UNRELATED_SECRET",
            "must-not-appear");

        var preamble = Build(EnvironmentVariableLogMode.AllValues);

        Assert.DoesNotContain("ZZ_PREAMBLE_TEST_UNRELATED_SECRET", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-appear", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeIsRecordedInTheSectionHeading() =>
        Assert.Contains(
            "Environment Variables (mode: KnownSafeValues)",
            Build(EnvironmentVariableLogMode.KnownSafeValues),
            StringComparison.Ordinal);

    [Fact]
    public void ConfigurationWarnings_AreReportedInThePreamble()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_LOG_LEVEL"] = "chatty",
            })
            .Build();

        var options = new SelfDiagnosticsOptions(configuration) { LogToStdout = true };
        var preamble = SelfDiagnosticsPreamble.Build(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options));

        Assert.Contains("Configuration Warnings:", preamble, StringComparison.Ordinal);
        Assert.Contains("OTEL_LOG_LEVEL", preamble, StringComparison.Ordinal);
        Assert.Contains("chatty", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void LogDirectory_IsReportedInThePreambleWhenConfigured()
    {
        const string Directory = "/var/log/otel";
        var preamble = SelfDiagnosticsPreamble.Build(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
                new SelfDiagnosticsOptions
                {
                    LogDirectory = Directory,
                    EnvironmentVariables = EnvironmentVariableLogMode.None,
                }));

        Assert.Contains($"Log directory        : {Directory}", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void LogDirectory_IsOmittedFromThePreambleWhenNotConfigured()
    {
        var preamble = Build(EnvironmentVariableLogMode.None);

        Assert.DoesNotContain("Log directory", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void NoConfigurationWarnings_OmitsTheSection() =>
        Assert.DoesNotContain(
            "Configuration Warnings:",
            Build(EnvironmentVariableLogMode.KnownSafeValues),
            StringComparison.Ordinal);

    [Fact]
    public void RuntimeEnvVarsSection_PresentWhenModeIsNotNone()
    {
        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);
        Assert.Contains("Runtime environment variables:", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfilerSection_ShowsNoneSetWhenNoProfilerVarsPresent()
    {
        var cleared = new Dictionary<string, string?>
        {
            ["COR_ENABLE_PROFILING"] = null,
            ["COR_PROFILER"] = null,
            ["COR_PROFILER_PATH_32"] = null,
            ["COR_PROFILER_PATH_64"] = null,
            ["CORECLR_ENABLE_PROFILING"] = null,
            ["CORECLR_PROFILER"] = null,
            ["CORECLR_PROFILER_PATH"] = null,
            ["CORECLR_PROFILER_PATH_32"] = null,
            ["CORECLR_PROFILER_PATH_64"] = null,
            ["DOTNET_STARTUP_HOOKS"] = null,
            ["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = null,
            ["DOTNET_ENVIRONMENT"] = null,
            ["ASPNETCORE_ENVIRONMENT"] = null,
            ["DOTNET_RUNNING_IN_CONTAINER"] = null,
        };

        using var scope = EnvironmentVariableScope.Create(cleared);

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains("(none set)", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeEnvVarsSection_ShowsValuesVerbatimRegardlessOfNonNoneMode()
    {
        const string guid = "{918728DD-259F-4A6A-AC2B-B85E1B658318}";
        using var scope = EnvironmentVariableScope.Create(
            ("CORECLR_ENABLE_PROFILING", "1"),
            ("CORECLR_PROFILER", guid));

        var preamble = Build(EnvironmentVariableLogMode.Names);

        Assert.Contains($"CORECLR_ENABLE_PROFILING = 1", preamble, StringComparison.Ordinal);
        Assert.Contains($"CORECLR_PROFILER = {guid}", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeEnvVarsSection_IncludesCOMPlusLoaderOptimization()
    {
        using var scope = EnvironmentVariableScope.Create("COMPlus_LoaderOptimization", "1");

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains("COMPlus_LoaderOptimization = 1", preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfilerSection_OmitsUnsetVars()
    {
        using var scope = EnvironmentVariableScope.Create(
            ("CORECLR_ENABLE_PROFILING", "1"),
            ("COR_ENABLE_PROFILING", (string?)null));

        var preamble = Build(EnvironmentVariableLogMode.KnownSafeValues);

        Assert.Contains("CORECLR_ENABLE_PROFILING", preamble, StringComparison.Ordinal);
        Assert.DoesNotContain("COR_ENABLE_PROFILING", preamble, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("A", null, null, "process")] // process-only: no registry entry for this var
    [InlineData("A", "B", "A", "user")] // process inherited user value (user shadows machine)
    [InlineData("A", "A", null, "system")] // process inherited machine value; no user override
    [InlineData("A", "B", null, "process")] // process value differs from machine registry value
    public void ClassifySource_ReturnsExpectedScope(
        string processValue,
        string? machineValue,
        string? userValue,
        string expected)
    {
        var result = SelfDiagnosticsPreamble.ClassifySource(processValue, machineValue, userValue);

        Assert.Equal(expected, result);
    }

    private static string Build(EnvironmentVariableLogMode mode) =>
        SelfDiagnosticsPreamble.Build(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
                new SelfDiagnosticsOptions
                {
                    LogToStdout = true,
                    EnvironmentVariables = mode,
                }));
}
