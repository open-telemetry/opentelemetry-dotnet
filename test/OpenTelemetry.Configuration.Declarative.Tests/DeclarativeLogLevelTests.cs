// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.SelfDiagnostics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class DeclarativeLogLevelTests
{
    public static TheoryData<string, LogLevel> SchemaValues()
    {
        var data = new TheoryData<string, LogLevel>();
        (string Name, LogLevel Level)[] bands =
        [
            ("trace", LogLevel.Trace),
            ("debug", LogLevel.Debug),
            ("info", LogLevel.Information),
            ("warn", LogLevel.Warning),
            ("error", LogLevel.Error),
            ("fatal", LogLevel.Critical),
        ];

        foreach (var (name, level) in bands)
        {
            data.Add(name, level);
            for (var i = 2; i <= 4; i++)
            {
                data.Add(name + i, level);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SchemaValues))]
    public void Read_SchemaValue_ParsesToSeverityThatMapsToLogLevel(string value, LogLevel expected)
    {
        using var yamlFile = CreateYaml($"log_level: {value}");

        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path));

        Assert.True(document.Model.LogLevel.TryGetValue(out var severity), "Expected log level to be present in the document.");
        Assert.Equal(value, severity.ToString(), ignoreCase: true);
        Assert.Equal(expected, severity.ToLogLevel());
    }

    [Fact]
    public void SchemaValues_CoverEveryValueTheSchemaDefines() => Assert.Equal(24, SchemaValues().Count);

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-1)]
    public void ToLogLevel_UndefinedSeverity_Throws(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SeverityNumber)value).ToLogLevel());

    [Fact]
    public void HostApplicationBuilderPath_SetsMinimumLevel()
    {
        using var yamlFile = CreateYaml("log_level: debug");

        using var host = BuildHost(yamlFile.Path);

        Assert.Equal(LogLevel.Debug, MinimumLevel(host.Services));
    }

    [Fact]
    public void ServiceCollectionPath_SetsMinimumLevel()
    {
        using var yamlFile = CreateYaml("log_level: debug");
        var services = new ServiceCollection();
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path).WithTracing();

        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();

        Assert.Equal(LogLevel.Debug, MinimumLevel(serviceProvider));
    }

    [Theory]
    [InlineData("")]
    [InlineData("log_level:")]
    public void NoLogLevel_LeavesCoreDefault(string line)
    {
        using var yamlFile = CreateYaml(line);

        using var host = BuildHost(yamlFile.Path);

        Assert.Equal(LogLevel.Warning, MinimumLevel(host.Services));
    }

    [Theory]
    [InlineData("", LogLevel.Warning)]
    [InlineData("log_level: error", LogLevel.Error)]
    public void StrictMode_HidesOtelLogLevelVariable(string line, LogLevel expected)
    {
        using var scope = EnvironmentVariableScope.Create("OTEL_LOG_LEVEL", "debug");
        using var yamlFile = CreateYaml(line);

        using var host = BuildHost(yamlFile.Path);

        Assert.Equal(expected, MinimumLevel(host.Services));
    }

    [Fact]
    public void AppSettingsLevel_IsOverriddenByTheDocument()
    {
        using var yamlFile = CreateYaml("log_level: warn");

        using var host = BuildHost(
            yamlFile.Path,
            configuration: new Dictionary<string, string?> { ["OpenTelemetry:SelfDiagnostics:MinimumLevel"] = "Trace" });

        Assert.Equal(LogLevel.Warning, MinimumLevel(host.Services));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CodeConfiguration_WinsWhereverItIsRegistered(bool registeredBefore)
    {
        using var yamlFile = CreateYaml("log_level: debug");
        Action<IServiceCollection> configure = s => s.Configure<SelfDiagnosticsOptions>(o => o.MinimumLevel = LogLevel.Error);

        using var host = BuildHost(
            yamlFile.Path,
            before: registeredBefore ? configure : null,
            after: registeredBefore ? null : configure);

        Assert.Equal(LogLevel.Error, MinimumLevel(host.Services));
    }

    [Theory]
    [InlineData("INFO", "info")]
    [InlineData("Debug", "debug")]
    [InlineData("none", null)]
    [InlineData("Warning", null)]
    [InlineData("verbose", null)]
    public void Read_ValueOutsideTheSchema_ThrowsNamingTheValueTheAllowedValuesAndTheFix(string value, string? suggestion)
    {
        using var yamlFile = CreateYaml($"log_level: {value}");

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path)));

        Assert.Contains("'log_level'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{value}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("line 2, column 12", exception.Message, StringComparison.Ordinal);
        Assert.Contains("trace, trace2, trace3, trace4, debug", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fatal3, fatal4", exception.Message, StringComparison.Ordinal);
        Assert.Equal(suggestion is not null, exception.Message.Contains("Values are lowercase", StringComparison.Ordinal));
        if (suggestion is not null)
        {
            Assert.Contains($"did you mean '{suggestion}'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("${OTEL_LOG_LEVEL}", exception.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("log_level: 5")]
    [InlineData("log_level:\n  level: info")]
    [InlineData("log_level: true")]
    public void Read_ValueOfTheWrongType_Throws(string line)
    {
        using var yamlFile = CreateYaml(line);

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path)));

        Assert.Contains("'log_level'", exception.Message, StringComparison.Ordinal);
        Assert.Matches(@"line \d+, column \d+", exception.Message);
        Assert.Contains("must be a string", exception.Message, StringComparison.Ordinal);
        Assert.Contains("debug2, debug3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_SubstitutedUppercaseValue_FailsLikeALiteralOne()
    {
        using var yamlFile = CreateYaml("log_level: ${OTEL_LOG_LEVEL}");

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => DeclarativeConfigurationReader.Read(
                new FilePath(yamlFile.Path),
                name => name == "OTEL_LOG_LEVEL" ? "INFO" : null));

        Assert.Contains("'INFO'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("did you mean 'info'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reload_KeepsTheDocumentLevelAndCodeStillWins()
    {
        using var yamlFile = CreateYaml("log_level: warn");

        using var withoutCode = BuildHost(yamlFile.Path, configuration: AppSettings());
        AssertReloadKeeps(withoutCode, LogLevel.Warning);

        using var withCode = BuildHost(
            yamlFile.Path,
            after: s => s.Configure<SelfDiagnosticsOptions>(o => o.MinimumLevel = LogLevel.Error),
            configuration: AppSettings());
        AssertReloadKeeps(withCode, LogLevel.Error);

        static Dictionary<string, string?> AppSettings() => new() { ["OpenTelemetry:SelfDiagnostics:MinimumLevel"] = "Information" };

        static void AssertReloadKeeps(IHost host, LogLevel expected)
        {
            var root = (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();
            var monitor = host.Services.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>();
            Assert.Equal(expected, monitor.CurrentValue.MinimumLevel);

            // The declarative source is read-only, so change the in-memory source directly and reload.
            var memory = root.Providers.OfType<MemoryConfigurationProvider>().Single(p => p.TryGet("OpenTelemetry:SelfDiagnostics:MinimumLevel", out _));
            memory.Set("OpenTelemetry:SelfDiagnostics:MinimumLevel", "Trace");
            memory.Set("OpenTelemetry:SelfDiagnostics:FileSizeLimitKilobytes", "123");
            root.Reload();

            Assert.True(
                SpinWait.SpinUntil(() => monitor.CurrentValue.FileSizeLimitKilobytes == 123, TimeSpan.FromSeconds(10)),
                "The options were not rebuilt after the configuration changed.");
            Assert.Equal(expected, monitor.CurrentValue.MinimumLevel);
        }
    }

    [Fact]
    public void LogLevel_IsNoLongerReportedAsNotApplied()
    {
        using var yamlFile = CreateYaml("log_level: info\nattribute_limits:\n  attribute_count_limit: 1");

        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path));

        Assert.DoesNotContain(document.UnappliedProperties, p => p.Path == "log_level");
        Assert.Contains(document.UnappliedProperties, p => p.Path == "attribute_limits");
    }

    [Fact]
    public void UseDeclarativeConfiguration_CalledTwice_AddsTheCallbacksOnce()
    {
        using var yamlFile = CreateYaml("log_level: debug");
        var services = new ServiceCollection();
        var builder = services.AddOpenTelemetry();

        builder.UseDeclarativeConfiguration(yamlFile.Path);
        var configureCount = Count<IConfigureOptions<SelfDiagnosticsOptions>>(services);

        builder.UseDeclarativeConfiguration(yamlFile.Path);

        Assert.Equal(1, configureCount);
        Assert.Equal(configureCount, Count<IConfigureOptions<SelfDiagnosticsOptions>>(services));

        static int Count<T>(IServiceCollection services) => services.Count(d => d.ServiceType == typeof(T));
    }

    [Fact]
    public void Configure_DocumentThatFailedToRead_LeavesTheOptionsUntouched()
    {
        using var yamlFile = CreateYaml("log_level: not-a-level");
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var options = new SelfDiagnosticsOptions { MinimumLevel = LogLevel.Error };

        // The accessor caches the failure, so this is the same exception the pipeline already surfaced.
        Assert.Throws<DeclarativeConfigurationException>(accessor.GetDocument);

        new DocumentLogLevelConfigureOptions(accessor).Configure(options);

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
    }

    [Fact]
    public void Configure_DocumentWithoutLogLevel_LeavesTheOptionsUntouched()
    {
        using var yamlFile = CreateYaml(string.Empty);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var options = new SelfDiagnosticsOptions { MinimumLevel = LogLevel.Error };

        new DocumentLogLevelConfigureOptions(accessor).Configure(options);

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
    }

    internal static IHost BuildHost(
        string yamlPath,
        Action<IServiceCollection>? before = null,
        Action<IServiceCollection>? after = null,
        IDictionary<string, string?>? configuration = null,
        bool allSignals = false)
    {
        var builder = Host.CreateApplicationBuilder();
        if (configuration is not null)
        {
            builder.Configuration.AddInMemoryCollection(configuration);
        }

        before?.Invoke(builder.Services);

        var otel = builder.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath).WithTracing();
        if (allSignals)
        {
            otel.WithMetrics().WithLogging();
        }

        after?.Invoke(builder.Services);

        var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();
        if (allSignals)
        {
            _ = host.Services.GetRequiredService<MeterProvider>();
            _ = host.Services.GetRequiredService<LoggerProvider>();
        }

        return host;
    }

    internal static DeclarativeYamlTestFile CreateYaml(string body) =>
        DeclarativeYamlTestFile.CreateYamlFile($"file_format: \"1.0\"\n{body}\n");

    private static LogLevel MinimumLevel(IServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>().CurrentValue.MinimumLevel;
}
