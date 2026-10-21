// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Diagnostics;

[Collection(EnvVarsCollectionDefinition.Name)]
public class SelfDiagnosticsOptionsBindingTests
{
    private const string SectionPath = "OpenTelemetry:SelfDiagnostics";

    public static TheoryData<string, LogLevel> MinimumLevelValues => new()
    {
        { "Trace", LogLevel.Trace },
        { "Debug", LogLevel.Debug },
        { "Information", LogLevel.Information },
        { "Warning", LogLevel.Warning },
        { "Error", LogLevel.Error },
        { "Critical", LogLevel.Critical },
        { "None", LogLevel.None },
        { "debug", LogLevel.Debug },
        { "warn", LogLevel.Warning },
        { "iNfO", LogLevel.Information },
        { " WARNING ", LogLevel.Warning },
        { " information ", LogLevel.Information },
    };

    [Theory]
    [MemberData(nameof(MinimumLevelValues))]
    public void MinimumLevel_NamesAndAliasesBindWithGeneratedAndReflectionBinders(string value, LogLevel expected)
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", value));

        Assert.Equal(expected, CreateDefaultOptions(configuration).MinimumLevel);

        var options = new SelfDiagnosticsOptions();
        ConfigurationBinder.Bind(CreateBindableSection(configuration), options);

        Assert.Equal(expected, options.MinimumLevel);
        Assert.Equal(value, configuration[$"{SectionPath}:MinimumLevel"]);
    }

    [Theory]
    [InlineData("None", EnvironmentVariableLogMode.None)]
    [InlineData("nAmEs", EnvironmentVariableLogMode.Names)]
    [InlineData("KnownSafeValues", EnvironmentVariableLogMode.KnownSafeValues)]
    [InlineData("knownsafe", EnvironmentVariableLogMode.KnownSafeValues)]
    [InlineData("AllValues", EnvironmentVariableLogMode.AllValues)]
    [InlineData("aLl", EnvironmentVariableLogMode.AllValues)]
    [InlineData(" all ", EnvironmentVariableLogMode.AllValues)]
    [InlineData(" Names ", EnvironmentVariableLogMode.Names)]
    public void EnvironmentVariables_AcceptsNamesAndAliases(string value, EnvironmentVariableLogMode expected)
    {
        var configuration = BuildConfiguration(($"{SectionPath}:EnvironmentVariables", value));

        Assert.Equal(expected, CreateDefaultOptions(configuration).EnvironmentVariables);

        var options = new SelfDiagnosticsOptions();
        ConfigurationBinder.Bind(CreateBindableSection(configuration), options);

        Assert.Equal(expected, options.EnvironmentVariables);
    }

    [Fact]
    public void SectionAbsent_OptionsKeepTheirDefaults()
    {
        var options = CreateDefaultOptions(BuildConfiguration());
        var defaults = new SelfDiagnosticsOptions();

        Assert.Equal(defaults.MinimumLevel, options.MinimumLevel);
        Assert.Equal(defaults.LogDirectory, options.LogDirectory);
        Assert.Equal(defaults.FileSizeLimitKilobytes, options.FileSizeLimitKilobytes);
        Assert.Equal(defaults.MaxRetainedFiles, options.MaxRetainedFiles);
        Assert.Equal(defaults.LogToStdout, options.LogToStdout);
        Assert.Equal(defaults.LogToStderr, options.LogToStderr);
        Assert.Equal(defaults.EnvironmentVariables, options.EnvironmentVariables);
    }

    [Theory]
    [InlineData("minimumlevel", "info", "Information")]
    [InlineData("MINIMUMLEVEL", "warn", "Warning")]
    [InlineData("environmentvariables", "all", "AllValues")]
    [InlineData("ENVIRONMENTVARIABLES", "knownsafe", "KnownSafeValues")]
    public void SectionNormalization_MatchesAliasKeysCaseInsensitively(string key, string value, string expected)
    {
        var configuration = BuildConfiguration(($"{SectionPath}:{key}", value));

        var section = CreateBindableSection(configuration);

        Assert.Equal(expected, section[key]);
        Assert.Equal(value, configuration[$"{SectionPath}:{key}"]);
    }

    [Fact]
    public void Section_BindsEveryOption()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            ($"{SectionPath}:MinimumLevel", "Debug"),
            ($"{SectionPath}:LogDirectory", "/var/log/otel"),
            ($"{SectionPath}:FileSizeLimitKilobytes", "4096"),
            ($"{SectionPath}:MaxRetainedFiles", "5"),
            ($"{SectionPath}:LogToStdout", "true"),
            ($"{SectionPath}:LogToStderr", "true"),
            ($"{SectionPath}:EnvironmentVariables", "Names")));

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
        Assert.Equal("/var/log/otel", options.LogDirectory);
        Assert.Equal(4096, options.FileSizeLimitKilobytes);
        Assert.Equal(5, options.MaxRetainedFiles);
        Assert.True(options.LogToStdout, "LogToStdout should be true.");
        Assert.True(options.LogToStderr, "LogToStderr should be true.");
        Assert.Equal(EnvironmentVariableLogMode.Names, options.EnvironmentVariables);
    }

    [Fact]
    public void RawReflectionBinding_DoesNotGainSdkAliases()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", "info"));

        Assert.Throws<InvalidOperationException>(() => ConfigurationBinder.Bind(configuration.GetSection(SectionPath), new SelfDiagnosticsOptions()));
    }

    [Theory]
    [InlineData("MinimumLevel", "2")]
    [InlineData("EnvironmentVariables", "2")]
    [InlineData("FileSizeLimitKilobytes", "0")]
    [InlineData("MaxRetainedFiles", "-1")]
    [InlineData("LogDirectory", "")]
    [InlineData("LogDirectory", "  ")]
    public void AutomaticBinding_PreservesOrdinaryBinderValues(string key, string value)
    {
        var configuration = BuildConfiguration(($"{SectionPath}:{key}", value));
        var expected = new SelfDiagnosticsOptions();
        ConfigurationBinder.Bind(configuration.GetSection(SectionPath), expected);

        var actual = CreateDefaultOptions(configuration);

        Assert.Equal(expected.MinimumLevel, actual.MinimumLevel);
        Assert.Equal(expected.EnvironmentVariables, actual.EnvironmentVariables);
        Assert.Equal(expected.FileSizeLimitKilobytes, actual.FileSizeLimitKilobytes);
        Assert.Equal(expected.MaxRetainedFiles, actual.MaxRetainedFiles);
        Assert.Equal(expected.LogDirectory, actual.LogDirectory);
    }

    [Fact]
    public void SameProvider_SectionKeysWinTies()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            (SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"),
            (SelfDiagnosticsOptions.LogDirectoryEnvVarName, "/from-environment"),
            (SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, "all"),
            ($"{SectionPath}:MinimumLevel", "Error"),
            ($"{SectionPath}:LogDirectory", "/from-section"),
            ($"{SectionPath}:EnvironmentVariables", "Names")));

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
        Assert.Equal("/from-section", options.LogDirectory);
        Assert.Equal(EnvironmentVariableLogMode.Names, options.EnvironmentVariables);
    }

    [Fact]
    public void SameProvider_SectionKeyCanTurnOffASinkTheVariableSelected()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            (SelfDiagnosticsOptions.SinksEnvVarName, "stdout,stderr"),
            ($"{SectionPath}:LogToStdout", "false")));

        Assert.False(options.LogToStdout, "Expected LogToStdout to be false when the section key disables it.");
        Assert.True(options.LogToStderr, "LogToStderr should remain true because the environment variable enabled it and the section does not set it.");
    }

    [Fact]
    public void VariableFromLaterProvider_BeatsSectionKey()
    {
        // Platform injects the variable; a checked-in file has the section.
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider: [($"{SectionPath}:MinimumLevel", "Error")],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]));

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
    }

    [Fact]
    public void SectionKeyFromLaterProvider_BeatsVariable()
    {
        // The variable is set in the environment; the command line overrides the section key.
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")],
            laterProvider: [($"{SectionPath}:MinimumLevel", "Error")]));

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
    }

    [Fact]
    public void ConfigurationManager_VariableFromLaterProvider_BeatsSectionKey()
    {
        // Hosts register a ConfigurationManager rather than a ConfigurationRoot.
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection([new($"{SectionPath}:MinimumLevel", "Error")]);
        configuration.AddInMemoryCollection([new(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]);

        Assert.Equal(LogLevel.Debug, CreateDefaultOptions(configuration).MinimumLevel);
    }

    [Fact]
    public void ChainedConfiguration_UsesItsOuterProviderPosition()
    {
        var chained = BuildConfiguration(
            earlierProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")],
            laterProvider: [($"{SectionPath}:MinimumLevel", "Error")]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new($"{SectionPath}:MinimumLevel", "Critical")])
            .AddConfiguration(chained)
            .Build();

        Assert.Equal(LogLevel.Error, CreateDefaultOptions(configuration).MinimumLevel);
    }

    [Theory]
    [InlineData(SelfDiagnosticsOptions.LogLevelEnvVarName, LogLevel.Error)]
    [InlineData("OpenTelemetry:SelfDiagnostics:MinimumLevel", LogLevel.Debug)]
    public void MaskedConfiguration_DoesNotExposeHiddenProviderValues(string maskedKey, LogLevel expected)
    {
        var configuration = new MaskedConfiguration(
            BuildConfiguration(
                earlierProvider: [($"{SectionPath}:MinimumLevel", "Error")],
                laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]),
            maskedKey);

        Assert.Equal(expected, CreateDefaultOptions(configuration).MinimumLevel);
    }

    [Theory]
    [InlineData("all", EnvironmentVariableLogMode.AllValues, 0)]
    [InlineData("unknown", EnvironmentVariableLogMode.Names, 1)]
    [InlineData("", EnvironmentVariableLogMode.Names, 0)]
    public void LaterDisclosureVariable_OnlyWinsWhenAccepted(string value, EnvironmentVariableLogMode expected, int warnings)
    {
        var configuration = BuildConfiguration(
            earlierProvider: [($"{SectionPath}:EnvironmentVariables", "Names")],
            laterProvider: [(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, value)]);

        var options = CreateDefaultOptions(configuration);

        Assert.Equal(expected, options.EnvironmentVariables);
        Assert.Equal(warnings, options.ConfigurationWarnings.Count);
    }

    [Fact]
    public void SinksNoneFromLaterProvider_TurnsOffSinksTheSectionEnabled()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:LogToStdout", "true"),
                ($"{SectionPath}:LogDirectory", "/from-section"),
            ],
            laterProvider: [(SelfDiagnosticsOptions.SinksEnvVarName, "none")]));

        Assert.False(options.LogToStdout, "LogToStdout should be false when sinks is set to 'none'.");
        Assert.Null(options.LogDirectory);
    }

    [Theory]
    [InlineData("logdirectory")]
    [InlineData("LOGDIRECTORY")]
    public void SectionDirectory_IsNotBoundOverTheSinkSelection_WhateverTheKeyCasing(string key)
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider: [($"{SectionPath}:{key}", "/from-section")],
            laterProvider: [(SelfDiagnosticsOptions.SinksEnvVarName, "none")]));

        Assert.Null(options.LogDirectory);
    }

    [Fact]
    public void SinksFromLaterProvider_IsACompleteSelection()
    {
        // The sinks variable decides every sink option, including the ones it does not name.
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:LogToStderr", "true"),
                ($"{SectionPath}:LogDirectory", "/from-section"),
            ],
            laterProvider: [(SelfDiagnosticsOptions.SinksEnvVarName, "stdout")]));

        Assert.True(options.LogToStdout, "LogToStdout should be true when sinks is set to 'stdout'.");
        Assert.False(options.LogToStderr, "LogToStderr should be false when sinks is set to 'stdout'.");
        Assert.Null(options.LogDirectory);
    }

    [Fact]
    public void VariableFromLaterProvider_OverridesOnlyItsOwnKey()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:MinimumLevel", "Error"),
                ($"{SectionPath}:MaxRetainedFiles", "5"),
                ($"{SectionPath}:LogToStderr", "true"),
            ],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]));

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
        Assert.Equal(5, options.MaxRetainedFiles);
        Assert.True(options.LogToStderr, "LogToStderr should be true when the section key enables it.");
    }

    [Theory]
    [InlineData("minimumlevel", "environmentvariables", "maxretainedfiles")]
    [InlineData("MINIMUMLEVEL", "ENVIRONMENTVARIABLES", "MAXRETAINEDFILES")]
    public void FilteredSection_MatchesKeysCaseInsensitively(string overriddenKey, string normalizedKey, string retainedKey)
    {
        var configuration = BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:{overriddenKey}", "verbose"),
                ($"{SectionPath}:{normalizedKey}", "all"),
                ($"{SectionPath}:{retainedKey}", "5"),
            ],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]);

        var options = CreateDefaultOptions(configuration);

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
        Assert.Equal(EnvironmentVariableLogMode.AllValues, options.EnvironmentVariables);
        Assert.Equal(5, options.MaxRetainedFiles);
        Assert.Equal("verbose", configuration[$"{SectionPath}:{overriddenKey}"]);
        Assert.Equal("all", configuration[$"{SectionPath}:{normalizedKey}"]);
    }

    [Theory]
    [InlineData("Error", LogLevel.Error)]
    [InlineData(null, LogLevel.Warning)]
    public void NullVariableFromLaterProvider_DoesNotRevealEarlierValue(string? sectionValue, LogLevel expected)
    {
        var configuration = BuildConfiguration(
            earlierProvider:
            [
                (SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"),
                ($"{SectionPath}:MinimumLevel", sectionValue),
            ],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, null)]);

        var options = CreateDefaultOptions(configuration);

        Assert.Equal(expected, options.MinimumLevel);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void LosingSectionValue_IsNotBound_EvenWhenInvalid()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider: [($"{SectionPath}:MinimumLevel", "verbose")],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]));

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
    }

    [Fact]
    public void WinningInvalidSectionValue_ThrowsAndNamesTheKey_WhenAnotherKeyLoses()
    {
        // Another key losing makes the SDK bind a copy of the section; errors still name the
        // original key.
        var configuration = BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:MinimumLevel", "Error"),
                ($"{SectionPath}:LogToStdout", "yes"),
            ],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateDefaultOptions(configuration));

        Assert.Contains($"{SectionPath}:LogToStdout", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("")]
    [InlineData("  ")]
    public void IgnoredVariableFromLaterProvider_LetsSectionValueApply(string ignoredValue)
    {
        // The options type ignores an unrecognised or blank OTEL_LOG_LEVEL, so it cannot win.
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider: [($"{SectionPath}:MinimumLevel", "Error")],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, ignoredValue)]));

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
    }

    [Fact]
    public void ConfigurationWithoutProviders_SectionKeysWin()
    {
        // Provider order is unknown, so every comparison is treated as a tie.
        var configuration = new OpaqueConfiguration(BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:MinimumLevel", "Error"),
                ($"{SectionPath}:LogDirectory", "/from-section"),
                ($"{SectionPath}:LogToStdout", "true"),
            ],
            laterProvider:
            [
                (SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"),
                (SelfDiagnosticsOptions.LogDirectoryEnvVarName, "/from-environment"),
                (SelfDiagnosticsOptions.SinksEnvVarName, "stderr"),
            ]));

        var options = CreateDefaultOptions(configuration);

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
        Assert.Equal("/from-section", options.LogDirectory);
        Assert.True(options.LogToStdout, "LogToStdout should be true when the section key enables it.");
        Assert.True(options.LogToStderr, "LogToStderr should be true when the environment variable enables it.");
    }

    [Fact]
    public void EnvironmentVariables_StillApplyToOptionsTheSectionDoesNotSet()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            (SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"),
            ($"{SectionPath}:LogToStderr", "true")));

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
        Assert.True(options.LogToStderr, "LogToStderr should be true when the section key enables it.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Configure_OverridesSectionBinding_WhicheverOrderItIsRegistered(bool registerBeforeSdkServices)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(($"{SectionPath}:MinimumLevel", "Debug")));

        if (registerBeforeSdkServices)
        {
            services.Configure<SelfDiagnosticsOptions>(o => o.MinimumLevel = LogLevel.Error);
        }

        services.AddOpenTelemetrySharedProviderBuilderServices();

        if (!registerBeforeSdkServices)
        {
            services.Configure<SelfDiagnosticsOptions>(o => o.MinimumLevel = LogLevel.Error);
        }

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>().CurrentValue;

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
    }

    [Fact]
    public void NamedOptions_IgnoreTheSection()
    {
        var configuration = BuildConfiguration(
            ($"{SectionPath}:MinimumLevel", "Debug"),
            ($"{SectionPath}:FileSizeLimitKilobytes", "not-a-number"));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var serviceProvider = services.BuildServiceProvider();

        // A non-default name is not bound, so neither the valid nor the invalid value applies.
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>().Get("custom-name");

        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Equal(SelfDiagnosticsOptions.DefaultFileSizeLimitKilobytes, options.FileSizeLimitKilobytes);
    }

    [Theory]
    [InlineData("MinimumLevel", "verbose")]
    [InlineData("EnvironmentVariables", "everything")]
    [InlineData("FileSizeLimitKilobytes", "ten")]
    [InlineData("LogToStdout", "yes")]
    public void InvalidValue_Throws(string key, string invalidValue)
    {
        var configuration = BuildConfiguration(($"{SectionPath}:{key}", invalidValue));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateDefaultOptions(configuration));

        Assert.Contains($"{SectionPath}:{key}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidValue_ThrowsWhenProviderIsBuilt()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", "verbose"));

        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(configuration));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void EmptyMinimumLevel_KeepsTheDefault()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", string.Empty));

#if NET8_0
        // The source-generated binder in Microsoft.Extensions.Configuration.Binder 8.0.2 does not
        // skip an empty enum value, unlike the generator in later versions.
        Assert.Throws<InvalidOperationException>(() => CreateDefaultOptions(configuration));
#else
        Assert.Equal(LogLevel.Warning, CreateDefaultOptions(configuration).MinimumLevel);
#endif
    }

    [Fact]
    public void KeysThatAreNotOptions_AreIgnored()
    {
        // "Sinks" mirrors OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS but is not an option, so it enables
        // nothing. ConfigurationWarnings is internal and must not be settable from configuration.
        var options = CreateDefaultOptions(BuildConfiguration(
            ($"{SectionPath}:Sinks", "stdout"),
            ($"{SectionPath}:LogLevel", "Debug"),
            ($"{SectionPath}:EnvironmentVariablesLogMode", "AllValues"),
            ($"{SectionPath}:EnvironmentVariableLogMode", "AllValues"),
            ($"{SectionPath}:ConfigurationWarnings:0", "injected"),
            ($"{SectionPath}Extra:MinimumLevel", "Debug"),
            ("OpenTelemetry:ServiceName", "app-owned")));

        Assert.False(options.LogToStdout, "LogToStdout should be false because none of the section keys are options.");
        Assert.Empty(options.ConfigurationWarnings);
        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Equal(EnvironmentVariableLogMode.KnownSafeValues, options.EnvironmentVariables);
    }

    [Fact]
    public void SectionBinding_AppliesToTheOptionsTheProviderUses()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", "Debug"));
        IServiceProvider? capturedServices = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(configuration))
            .AddProcessor(sp =>
            {
                capturedServices = sp;
                return new SimpleActivityExportProcessor(new InMemoryExporter<Activity>([]));
            })
            .Build();

        var options = capturedServices!.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>().CurrentValue;

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
    }

    [Fact]
    public void SectionShapedEnvironmentVariable_BindsWithoutAHostConfiguration()
    {
        using var environment = EnvironmentVariableScope.Create("OpenTelemetry__SelfDiagnostics__MinimumLevel", "info");

        // No IConfiguration is registered, so the SDK's environment-only fallback configuration
        // supplies the section-shaped variable.
        var services = new ServiceCollection();
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>().CurrentValue;

        Assert.Equal(LogLevel.Information, options.MinimumLevel);
    }

    [Fact]
    public void SharedServices_CalledRepeatedly_RegisterOneFactoryAndDefaultNameChangeTokenSource()
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.AddOpenTelemetryTracerProviderBuilderServices();
        services.AddOpenTelemetryMeterProviderBuilderServices();
        services.AddOpenTelemetryLoggerProviderBuilderServices();
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.AddOpenTelemetrySharedProviderBuilderServices();

        Assert.Single(services, d => d.ServiceType == typeof(IOptionsFactory<SelfDiagnosticsOptions>));
        Assert.Single(services, d => d.ServiceType == typeof(IOptionsChangeTokenSource<SelfDiagnosticsOptions>));

        using var provider = services.BuildServiceProvider();

        Assert.Equal(Options.DefaultName, provider.GetRequiredService<IOptionsChangeTokenSource<SelfDiagnosticsOptions>>().Name);
    }

    [Fact]
    public void ExplicitSectionBinding_StillReloads()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", "Debug"));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.Configure<SelfDiagnosticsOptions>(configuration.GetSection(SectionPath));
        using var serviceProvider = services.BuildServiceProvider();

        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();

        Assert.Equal(LogLevel.Debug, monitor.CurrentValue.MinimumLevel);

        configuration[$"{SectionPath}:MinimumLevel"] = "Error";
        configuration.Reload();

        Assert.Equal(LogLevel.Error, monitor.CurrentValue.MinimumLevel);
    }

    [Theory]
    [InlineData("file", @"C:\section", false, false, 0)]
    [InlineData("file,stderr", @"C:\section", false, true, 0)]
    [InlineData("stdout", null, true, false, 0)]
    [InlineData("none", null, false, false, 0)]
    [InlineData("stdout,none,file", null, false, false, 0)]
    [InlineData(" CONSOLE ", null, true, true, 0)]
    [InlineData("syslog", @"C:\section", true, true, 1)]
    [InlineData(",,", @"C:\section", true, true, 0)]
    [InlineData("stderr,syslog", null, false, true, 1)]
    [InlineData("stdout,,stderr,", null, true, true, 0)]
    public void LaterSinks_ResolveSelectionWithoutLosingFileLocation(string sinks, string? directory, bool stdout, bool stderr, int warnings)
    {
        var configuration = BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:LogDirectory", @"C:\section"),
                ($"{SectionPath}:LogToStdout", "true"),
                ($"{SectionPath}:LogToStderr", "true"),
            ],
            laterProvider: [(SelfDiagnosticsOptions.SinksEnvVarName, sinks)]);

        var resolverCalls = 0;
        var options = SelfDiagnosticsOptionsFactory.CreateDefault(configuration, () =>
        {
            resolverCalls++;
            return @"C:\fallback";
        });

        Assert.Equal(directory, options.LogDirectory);
        Assert.Equal(stdout, options.LogToStdout);
        Assert.Equal(stderr, options.LogToStderr);
        Assert.Equal(0, resolverCalls);
        Assert.Equal(warnings, options.ConfigurationWarnings.Count);
    }

    [Theory]
    [InlineData("file", "", "")]
    [InlineData("none", @"C:\later", @"C:\later")]
    [InlineData("stdout", @"C:\later", @"C:\later")]
    [InlineData("file", @"C:\later", @"C:\later")]
    public void LaterSectionDirectory_OverridesSinkSelection(string sinks, string sectionDirectory, string expectedDirectory)
    {
        var configuration = BuildConfiguration(
            earlierProvider: [(SelfDiagnosticsOptions.SinksEnvVarName, sinks)],
            laterProvider: [($"{SectionPath}:LogDirectory", sectionDirectory)]);

        var options = SelfDiagnosticsOptionsFactory.CreateDefault(configuration, () => throw new InvalidOperationException("Unexpected fallback."));

        Assert.Equal(expectedDirectory, options.LogDirectory);
    }

    [Fact]
    public void LaterSectionStream_AddsToEarlierSelection()
    {
        var options = CreateDefaultOptions(BuildConfiguration(
            earlierProvider: [(SelfDiagnosticsOptions.SinksEnvVarName, "stdout")],
            laterProvider: [($"{SectionPath}:LogToStderr", "true")]));

        Assert.True(options.LogToStdout, "LogToStdout should be true when the environment variable enables it.");
        Assert.True(options.LogToStderr, "LogToStderr should be true when the section key enables it.");
    }

    [Theory]
    [InlineData("file", "")]
    [InlineData("none", @"C:\section")]
    public void SameProvider_SectionDirectoryAndStreamsWin(string sinks, string directory)
    {
        var options = SelfDiagnosticsOptionsFactory.CreateDefault(
            BuildConfiguration(
                (SelfDiagnosticsOptions.SinksEnvVarName, sinks),
                ($"{SectionPath}:LogDirectory", directory),
                ($"{SectionPath}:LogToStderr", "true")),
            () => throw new InvalidOperationException("Unexpected fallback."));

        Assert.Equal(directory, options.LogDirectory);
        Assert.True(options.LogToStderr, "LogToStderr should be true when the section key enables it.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileSelection_UsesLatestConfiguredLocation(bool sectionIsLater)
    {
        var section = new KeyValuePair<string, string?>($"{SectionPath}:LogDirectory", @"C:\section");
        var variable = new KeyValuePair<string, string?>(SelfDiagnosticsOptions.LogDirectoryEnvVarName, @"C:\variable");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([sectionIsLater ? variable : section])
            .AddInMemoryCollection([sectionIsLater ? section : variable])
            .AddInMemoryCollection([new(SelfDiagnosticsOptions.SinksEnvVarName, "file")])
            .Build();

        var options = SelfDiagnosticsOptionsFactory.CreateDefault(configuration, () => throw new InvalidOperationException("Unexpected fallback."));

        Assert.Equal(sectionIsLater ? @"C:\section" : @"C:\variable", options.LogDirectory);
    }

    [Theory]
    [InlineData(0, 1, 2, "file", @"C:\variable")]
    [InlineData(0, 2, 1, "file", @"C:\variable")]
    [InlineData(1, 0, 2, "file", @"C:\section")]
    [InlineData(2, 0, 1, "file", @"C:\section")]
    [InlineData(1, 2, 0, "file", @"C:\variable")]
    [InlineData(2, 1, 0, "file", @"C:\section")]
    [InlineData(0, 1, 2, "stdout", null)]
    [InlineData(0, 2, 1, "stdout", null)]
    [InlineData(1, 0, 2, "stdout", null)]
    [InlineData(2, 0, 1, "stdout", @"C:\section")]
    [InlineData(1, 2, 0, "stdout", null)]
    [InlineData(2, 1, 0, "stdout", @"C:\section")]
    [InlineData(0, 1, 2, "none", null)]
    [InlineData(0, 2, 1, "none", null)]
    [InlineData(1, 0, 2, "none", null)]
    [InlineData(2, 0, 1, "none", @"C:\section")]
    [InlineData(1, 2, 0, "none", null)]
    [InlineData(2, 1, 0, "none", @"C:\section")]
    public void ThreeProviders_ResolveDirectoryAndSinkSelection(
        int sectionProviderIndex,
        int variableProviderIndex,
        int sinksProviderIndex,
        string sinks,
        string? expectedDirectory)
    {
        var providers = new KeyValuePair<string, string?>[3][];
        providers[sectionProviderIndex] = [new($"{SectionPath}:LogDirectory", @"C:\section")];
        providers[variableProviderIndex] = [new(SelfDiagnosticsOptions.LogDirectoryEnvVarName, @"C:\variable")];
        providers[sinksProviderIndex] = [new(SelfDiagnosticsOptions.SinksEnvVarName, sinks)];

        var builder = new ConfigurationBuilder();
        foreach (var provider in providers)
        {
            builder.AddInMemoryCollection(provider);
        }

        var configuration = builder.Build();
        var options = SelfDiagnosticsOptionsFactory.CreateDefault(
            configuration,
            () => throw new InvalidOperationException("Unexpected fallback."));

        Assert.Equal(expectedDirectory, options.LogDirectory);
        Assert.Equal(string.Equals(sinks, "stdout", StringComparison.Ordinal), options.LogToStdout);
        Assert.False(options.LogToStderr, "Expected LogToStderr to be false when sinks is not 'stderr'.");
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Theory]
    [InlineData(@"C:\fallback")]
    [InlineData(null)]
    public void FileSelection_ResolvesAndReportsFallbackOnlyWhenNeeded(string? fallback)
    {
        var calls = 0;
        var reports = new List<string>();
        var options = SelfDiagnosticsOptionsFactory.CreateDefault(
            BuildConfiguration((SelfDiagnosticsOptions.SinksEnvVarName, "file")),
            () =>
            {
                calls++;
                return fallback;
            },
            reports.Add);

        Assert.Equal(1, calls);
        Assert.Equal(fallback, options.LogDirectory);
        Assert.Equal(fallback == null ? 1 : 0, reports.Count);
        Assert.Equal(reports, options.ConfigurationWarnings);
    }

    [Fact]
    public void IgnoredEffectiveVariable_DoesNotRevealEarlierValue()
    {
        var configuration = BuildConfiguration(
            earlierProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"), ($"{SectionPath}:MinimumLevel", "Error")],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "critical")]);

        var options = CreateDefaultOptions(configuration);

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
        Assert.Single(options.ConfigurationWarnings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AutomaticReload_RecreatesDefaultOptionsButNotNamedOptions(bool sectionInitiallyPresent)
    {
        var configuration = BuildConfiguration((SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"));

        if (sectionInitiallyPresent)
        {
            configuration[$"{SectionPath}:MinimumLevel"] = "info";
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();
        var named = monitor.Get("custom");
        var initial = monitor.CurrentValue;

        Assert.Equal(sectionInitiallyPresent ? LogLevel.Information : LogLevel.Debug, initial.MinimumLevel);

        configuration[$"{SectionPath}:MinimumLevel"] = "warn";
        configuration[$"{SectionPath}:LogToStdout"] = "true";
        configuration[$"{SectionPath}:LogDirectory"] = @"C:\reloaded";
        configuration.Reload();

        Assert.NotSame(initial, monitor.CurrentValue);
        Assert.Equal(LogLevel.Warning, monitor.CurrentValue.MinimumLevel);
        Assert.True(monitor.CurrentValue.LogToStdout, "LogToStdout should be true when the section key enables it.");
        Assert.Equal(@"C:\reloaded", monitor.CurrentValue.LogDirectory);
        Assert.Same(named, monitor.Get("custom"));

        configuration[$"{SectionPath}:MinimumLevel"] = null;
        configuration[$"{SectionPath}:LogToStdout"] = null;
        configuration[$"{SectionPath}:LogDirectory"] = null;
        configuration.Reload();

        Assert.Equal(LogLevel.Debug, monitor.CurrentValue.MinimumLevel);
        Assert.False(monitor.CurrentValue.LogToStdout, "LogToStdout should return to its default of false when the section key is removed.");
        Assert.Null(monitor.CurrentValue.LogDirectory);
    }

    [Fact]
    public void AutomaticReload_RecomputesFilteredAndNormalizedValues()
    {
        var configuration = BuildConfiguration(
            earlierProvider: [($"{SectionPath}:MinimumLevel", "info")],
            laterProvider: [(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug")]);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();

        Assert.Equal(LogLevel.Debug, monitor.CurrentValue.MinimumLevel);

        configuration.Providers.Last().Set(SelfDiagnosticsOptions.LogLevelEnvVarName, null);
        configuration.Reload();

        Assert.Equal(LogLevel.Information, monitor.CurrentValue.MinimumLevel);
    }

    [Fact]
    public void ApplicationNamedChangeTokenSource_IsPreservedAndRemainsIndependent()
    {
        var configuration = BuildConfiguration(
            (SelfDiagnosticsOptions.LogLevelEnvVarName, "debug"),
            ($"{SectionPath}:MinimumLevel", "Error"));
        var namedConfiguration = BuildConfiguration();
        var applicationSource = new ConfigurationChangeTokenSource<SelfDiagnosticsOptions>("custom", namedConfiguration);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IOptionsChangeTokenSource<SelfDiagnosticsOptions>>(applicationSource);
        services.Configure<SelfDiagnosticsOptions>("custom", o => o.LogToStderr = true);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var provider = services.BuildServiceProvider();

        var sources = provider.GetServices<IOptionsChangeTokenSource<SelfDiagnosticsOptions>>().ToArray();

        Assert.Equal(2, sources.Length);
        Assert.Contains(applicationSource, sources);

        var monitor = provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();
        var named = monitor.Get("custom");
        var initial = monitor.CurrentValue;

        Assert.Equal(LogLevel.Debug, named.MinimumLevel);
        Assert.True(named.LogToStderr, "LogToStderr should be true.");

        configuration.Reload();

        Assert.Same(named, monitor.Get("custom"));
        Assert.NotSame(initial, monitor.CurrentValue);

        initial = monitor.CurrentValue;
        namedConfiguration.Reload();

        Assert.NotSame(named, monitor.Get("custom"));
        Assert.Same(initial, monitor.CurrentValue);
    }

    [Fact]
    public void AutomaticReload_ReplaysCallbacksPostConfigurationAndValidation()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", "info"));
        var callbacks = 0;
        var validations = 0;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<SelfDiagnosticsOptions>(options =>
        {
            callbacks++;
            options.MinimumLevel = LogLevel.Debug;
            options.LogToStdout = false;
            options.LogToStderr = false;
            options.LogDirectory = null;
        });
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.PostConfigure<SelfDiagnosticsOptions>(options => options.MinimumLevel = LogLevel.Critical);
        services.AddOptions<SelfDiagnosticsOptions>().Validate(options =>
        {
            validations++;
            return options.MinimumLevel == LogLevel.Critical;
        });
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();

        Assert.Equal(LogLevel.Critical, monitor.CurrentValue.MinimumLevel);

        configuration[$"{SectionPath}:MinimumLevel"] = "warn";
        configuration[$"{SectionPath}:LogToStderr"] = "true";
        configuration.Reload();

        Assert.Equal(LogLevel.Critical, monitor.CurrentValue.MinimumLevel);
        Assert.False(monitor.CurrentValue.LogToStderr, "LogToStderr should be false because the Configure callback runs after section binding and resets it.");
        Assert.Equal(2, callbacks);
        Assert.Equal(2, validations);
    }

    [Fact]
    public void InvalidReload_SurfacesNormalOptionsFailure()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:MinimumLevel", "info"));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();

        Assert.Equal(LogLevel.Information, monitor.CurrentValue.MinimumLevel);

        configuration[$"{SectionPath}:MinimumLevel"] = "verbose";

        var exception = Assert.Throws<AggregateException>(configuration.Reload);

        Assert.Contains($"{SectionPath}:MinimumLevel", exception.ToString(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => monitor.CurrentValue);

        configuration[$"{SectionPath}:MinimumLevel"] = "warn";
        configuration.Reload();

        Assert.Equal(LogLevel.Warning, monitor.CurrentValue.MinimumLevel);
    }

    private static IConfigurationRoot BuildConfiguration(params (string Key, string? Value)[] settings)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static IConfigurationRoot BuildConfiguration(
        (string Key, string? Value)[] earlierProvider,
        (string Key, string? Value)[] laterProvider)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(earlierProvider.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .AddInMemoryCollection(laterProvider.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    // Only for configurations without variables, so no variable counts as applied.
    private static IConfiguration CreateBindableSection(IConfiguration configuration)
        => ConfigurationAliasPrecedence.CreateBindableSection(
            configuration,
            SectionPath,
            SelfDiagnosticsOptionsEnvironmentDefaults.SectionKeyAliases,
            static _ => false,
            SelfDiagnosticsOptionsEnvironmentDefaults.SectionKeysResolvedWithVariables);

    private static SelfDiagnosticsOptions CreateDefaultOptions(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        using var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>().Get(Options.DefaultName);
    }

    // An IConfiguration that is not an IConfigurationRoot, so its providers are not visible.
    private sealed class OpaqueConfiguration(IConfiguration inner) : IConfiguration
    {
        public string? this[string key]
        {
            get => inner[key];
            set => inner[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => inner.GetReloadToken();

        public IConfigurationSection GetSection(string key) => inner.GetSection(key);
    }

    private sealed class MaskedConfiguration(IConfigurationRoot inner, string maskedKey) : IConfigurationRoot
    {
        public IEnumerable<IConfigurationProvider> Providers => inner.Providers;

        public string? this[string key]
        {
            get => string.Equals(key, maskedKey, StringComparison.OrdinalIgnoreCase) ? null : inner[key];
            set => inner[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren().Select(child => this.GetSection(child.Key));

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => inner.GetReloadToken();

        public IConfigurationSection GetSection(string key) => new ConfigurationSection(this, key);

        public void Reload() => inner.Reload();
    }
}
