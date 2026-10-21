// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Internal.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public class ConfigurationAliasPrecedenceTests
{
    private const string SectionPath = "App:Feature";
    private const string LevelVariable = "TEST_LEVEL";
    private const string ModeVariable = "TEST_MODE";
    private const string Earlier = "earlier";
    private const string Later = "later";

    private static readonly ConfigurationAlias[] Aliases =
    [
        new("Level", LevelVariable, static value => value.ToUpperInvariant()),
        new("Mode", ModeVariable),
    ];

    private static readonly string[] ExcludedKeys = ["Path"];

    [Fact]
    public void CreateBindableSection_VariableFromLaterProvider_OmitsOnlyItsKey()
    {
        var configuration = BuildConfiguration(
            earlierProvider: [($"{SectionPath}:Level", "low"), ($"{SectionPath}:Mode", "fast")],
            laterProvider: [(LevelVariable, "high")]);

        Assert.Equal(["Mode=fast"], GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_SectionKeyFromLaterProvider_IsKept()
    {
        var configuration = BuildConfiguration(
            earlierProvider: [(LevelVariable, "high")],
            laterProvider: [($"{SectionPath}:Level", "low")]);

        Assert.Equal(["Level=LOW"], GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_SameProvider_SectionKeyIsKept()
    {
        var configuration = BuildConfiguration(
            (LevelVariable, "high"),
            ($"{SectionPath}:Level", "low"));

        Assert.Equal(["Level=LOW"], GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_HiddenProviders_SectionKeyIsKept()
    {
        var configuration = new OpaqueConfiguration(BuildConfiguration(
            earlierProvider: [($"{SectionPath}:Level", "low")],
            laterProvider: [(LevelVariable, "high")]));

        Assert.Equal(["Level=LOW"], GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_OnlyAppliedVariablesOmitTheirKeys()
    {
        var configuration = BuildConfiguration(
            earlierProvider: [($"{SectionPath}:Level", "low"), ($"{SectionPath}:Mode", "fast")],
            laterProvider: [(LevelVariable, "ignored"), (ModeVariable, "slow")]);

        var section = CreateBindableSection(configuration, isVariableApplied: static name => name == ModeVariable);

        Assert.Equal(["Level=LOW"], GetValues(section));
    }

    [Fact]
    public void CreateBindableSection_OmitsChildrenOfOverriddenAndExcludedKeys()
    {
        var configuration = BuildConfiguration(
            earlierProvider:
            [
                ($"{SectionPath}:level", "low"),
                ($"{SectionPath}:Level:Child", "child"),
                ($"{SectionPath}:PATH", "/from-section"),
                ($"{SectionPath}:Path:Child", "child"),
                ($"{SectionPath}:Other", "kept"),
            ],
            laterProvider: [(LevelVariable, "high")]);

        Assert.Equal(["Other=kept"], GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_NormalizesOnlyTheTopLevelKeyAnAliasNames()
    {
        var configuration = BuildConfiguration(
            ($"{SectionPath}:level", "low"),
            ($"{SectionPath}:level:Child", "low"),
            ($"{SectionPath}:Other:Level", "low"));

        Assert.Equal(
            ["Other:Level=low", "level:Child=low", "level=LOW"],
            GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_OmitsEmptyValues()
    {
        var configuration = BuildConfiguration(
            ($"{SectionPath}:Level", string.Empty),
            ($"{SectionPath}:Mode", "fast"),
            ($"{SectionPath}:Other", " "));

        Assert.Equal(["Mode=fast", "Other= "], GetValues(CreateBindableSection(configuration)));
    }

    [Fact]
    public void CreateBindableSection_KeepsTheOriginalKeyPaths()
    {
        var section = CreateBindableSection(BuildConfiguration(($"{SectionPath}:Mode", "fast")));

        Assert.Equal(SectionPath, Assert.IsAssignableFrom<IConfigurationSection>(section).Path);
        Assert.Equal($"{SectionPath}:Mode", section.GetSection("Mode").Path);
    }

    [Fact]
    public void CreateBindableSection_ReturnsASnapshot()
    {
        var configuration = BuildConfiguration(($"{SectionPath}:Mode", "fast"));
        var section = CreateBindableSection(configuration);

        configuration[$"{SectionPath}:Mode"] = "slow";

        Assert.Equal("fast", section["Mode"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateBindableSection_EnvironmentVariablesProvider_UsesItsPosition(bool environmentIsLater)
    {
        const string Prefix = "OTEL_TESTS_ALIAS_PRECEDENCE_";
        var fileSettings = new Dictionary<string, string?> { [$"{SectionPath}:Level"] = "low" };

        try
        {
            Environment.SetEnvironmentVariable(Prefix + LevelVariable, "high");
            Environment.SetEnvironmentVariable(Prefix + "App__Feature__Mode", "fast");

            var builder = new ConfigurationBuilder();
            if (environmentIsLater)
            {
                builder.AddInMemoryCollection(fileSettings).AddEnvironmentVariables(Prefix);
            }
            else
            {
                builder.AddEnvironmentVariables(Prefix).AddInMemoryCollection(fileSettings);
            }

            string[] expected = environmentIsLater ? ["Mode=fast"] : ["Level=LOW", "Mode=fast"];
            Assert.Equal(expected, GetValues(CreateBindableSection(builder.Build())));
        }
        finally
        {
            Environment.SetEnvironmentVariable(Prefix + LevelVariable, null);
            Environment.SetEnvironmentVariable(Prefix + "App__Feature__Mode", null);
        }
    }

    [Theory]
    [InlineData(Later, Earlier, true)]
    [InlineData(Earlier, Later, false)]
    [InlineData(Earlier, Earlier, false)]
    [InlineData(Later, Later, false)]
    [InlineData(Earlier, null, true)]
    [InlineData(null, Earlier, false)]
    [InlineData(null, null, false)]
    public void IsFromLaterProvider_ComparesTheProvidersThatSupplyEachKey(string? keyProvider, string? otherKeyProvider, bool expected)
    {
        var configuration = BuildConfiguration(SettingsIn(Earlier), SettingsIn(Later));

        Assert.Equal(expected, ConfigurationAliasPrecedence.IsFromLaterProvider(configuration, "Key", "OtherKey"));

        (string Key, string? Value)[] SettingsIn(string provider)
        {
            List<(string Key, string? Value)> settings = [];
            if (keyProvider == provider)
            {
                settings.Add(("Key", "value"));
            }

            if (otherKeyProvider == provider)
            {
                settings.Add(("OtherKey", "value"));
            }

            return [.. settings];
        }
    }

    [Fact]
    public void IsFromLaterProvider_NullValueStillComesFromItsProvider()
    {
        var configuration = BuildConfiguration(
            earlierProvider: [("Key", "value"), ("OtherKey", "value")],
            laterProvider: [("Key", null)]);

        Assert.True(ConfigurationAliasPrecedence.IsFromLaterProvider(configuration, "Key", "OtherKey"), "Key should be considered from the later provider even if its value is null.");
    }

    [Fact]
    public void IsFromLaterProvider_HiddenProviders_ReturnsFalse()
    {
        var configuration = new OpaqueConfiguration(BuildConfiguration(
            earlierProvider: [("OtherKey", "value")],
            laterProvider: [("Key", "value")]));

        Assert.False(ConfigurationAliasPrecedence.IsFromLaterProvider(configuration, "Key", "OtherKey"), "Key should not be considered from the later provider when the configuration is opaque.");
    }

    private static IConfiguration CreateBindableSection(IConfiguration configuration, Func<string, bool>? isVariableApplied = null)
        => ConfigurationAliasPrecedence.CreateBindableSection(
            configuration,
            SectionPath,
            Aliases,
            isVariableApplied ?? (static _ => true),
            ExcludedKeys);

    // The non-null values as sorted "key=value" strings, with keys relative to the section.
    private static string[] GetValues(IConfiguration section)
        =>
        [
            .. section.AsEnumerable(makePathsRelative: true)
                .Where(pair => pair.Value != null)
                .Select(pair => $"{pair.Key}={pair.Value}")
                .OrderBy(entry => entry, StringComparer.Ordinal),
        ];

    private static IConfigurationRoot BuildConfiguration(params (string Key, string? Value)[] settings)
        => BuildConfiguration(settings, []);

    private static IConfigurationRoot BuildConfiguration(
        (string Key, string? Value)[] earlierProvider,
        (string Key, string? Value)[] laterProvider)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(earlierProvider.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .AddInMemoryCollection(laterProvider.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    // An IConfiguration that is not an IConfigurationRoot, so its providers are not visible.
    private sealed class OpaqueConfiguration(IConfiguration inner) : IConfiguration
    {
        public string? this[string key]
        {
            get => inner[key];
            set => inner[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren();

        public IChangeToken GetReloadToken() => inner.GetReloadToken();

        public IConfigurationSection GetSection(string key) => inner.GetSection(key);
    }
}
