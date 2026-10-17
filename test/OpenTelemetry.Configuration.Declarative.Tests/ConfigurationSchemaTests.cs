// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Globalization;
#endif
using System.Security.Cryptography;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ConfigurationSchemaTests
{
    private const string PinnedSha256 = "B89640705B5248CD30D661C82506005074BC5655CF5FE3450B29F03FEE551D64";

    [Fact]
    public void PinnedResource_HasPinnedHash()
    {
        using var stream = OpenPinnedResource();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        // The schema contains no bare carriage returns, so dropping them normalizes line endings to LF.
        var normalized = buffer.ToArray().Where(value => value != (byte)'\r').ToArray();

#if NET
        var hash = Convert.ToHexString(SHA256.HashData(normalized));
#else
        using var sha256 = SHA256.Create();
        var hash = string.Concat(sha256.ComputeHash(normalized).Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
#endif

        Assert.Equal(PinnedSha256, hash);
    }

    [Fact]
    public void Pinned_RootDefinesTheSchemaRootProperties()
    {
        var root = ConfigurationSchema.Pinned.Root;

        string[] expected =
        [
            "attribute_limits",
            "disabled",
            "distribution",
            "file_format",
            "instrumentation/development",
            "log_level",
            "logger_provider",
            "meter_provider",
            "propagator",
            "resource",
            "tracer_provider",
        ];

        Assert.Equal(expected, root.Properties.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(AdditionalPropertiesKind.Allowed, root.AdditionalProperties);
        Assert.Equal(SchemaValueTypes.Object, root.ValueTypes);
    }

    [Fact]
    public void Pinned_ResolvesPropertyTypesAndAdditionalPropertiesKinds()
    {
        var root = ConfigurationSchema.Pinned.Root;

        var resource = root.Properties["resource"];
        Assert.Equal(SchemaValueTypes.Object, resource.ValueTypes);
        Assert.Equal(AdditionalPropertiesKind.Forbidden, resource.AdditionalProperties);

        var attributes = resource.Properties["attributes"];
        Assert.Equal(SchemaValueTypes.Array, attributes.ValueTypes);
        Assert.NotNull(attributes.Items);
        Assert.Equal(AdditionalPropertiesKind.Forbidden, attributes.Items.AdditionalProperties);

        var sampler = root.Properties["tracer_provider"].Properties["sampler"];
        Assert.Equal(AdditionalPropertiesKind.Schema, sampler.AdditionalProperties);
        Assert.NotNull(sampler.AdditionalPropertiesSchema);
    }

    [Fact]
    public void Pinned_RecursiveReferenceResolvesToTheSameNode()
    {
        var sampler = ConfigurationSchema.Pinned.Root.Properties["tracer_provider"].Properties["sampler"];

        var parentBased = sampler.Properties["parent_based"];

        Assert.Same(sampler, parentBased.Properties["root"]);
    }

    [Fact]
    public void Pinned_OnlyTheRootPermitsAdditionalPropertiesOnAnObjectWithProperties()
    {
        var root = ConfigurationSchema.Pinned.Root;

        var offenders = EnumerateNodes(root)
            .Where(node => node != root && node.Properties.Count > 0 && node.AdditionalProperties == AdditionalPropertiesKind.Allowed)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Pinned_OnlyAttributeValuesHaveOneOfAlternatives()
    {
        var root = ConfigurationSchema.Pinned.Root;
        var attribute = root.Properties["resource"].Properties["attributes"].Items;
        Assert.NotNull(attribute);

        var alternatives = Assert.Single(EnumerateNodes(root), node => node.HasOneOf);

        Assert.Same(attribute.Properties["value"], alternatives);
    }

    [Theory]
    [InlineData("x/development", true)]
    [InlineData("x/alpha", true)]
    [InlineData("x/beta", true)]
    [InlineData("instrumentation/development", true)]
    [InlineData("x", false)]
    [InlineData("development", false)]
    [InlineData("x/dev", false)]
    [InlineData("x/developmentx", false)]
    [InlineData("x/Development", false)]
    [InlineData("", false)]
    public void IsExperimentalPropertyName_MatchesTheSuffixRule(string name, bool expected) =>
        Assert.Equal(expected, ConfigurationSchema.IsExperimentalPropertyName(name));

    [Fact]
    public void Version_MinorMatchesMaxSupportedMinorVersion()
    {
        var version = Version.Parse(ConfigurationSchema.Version);

        Assert.Equal(FileFormatValidator.SupportedMajorVersion, version.Major);
        Assert.Equal(FileFormatValidator.MaxSupportedMinorVersion, version.Minor);
    }

    [Fact]
    public void Provenance_IsConsistent()
    {
        Assert.Equal("v" + ConfigurationSchema.Version, ConfigurationSchema.SourceTag);
        Assert.Contains(ConfigurationSchema.SourceTag, ConfigurationSchema.SourceUrl, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{40}$", ConfigurationSchema.SourceCommit);
    }

    private static Stream OpenPinnedResource() =>
        typeof(ConfigurationSchema).Assembly.GetManifestResourceStream(ConfigurationSchema.ResourceName)
        ?? throw new InvalidOperationException("The pinned schema resource was not found.");

    private static List<SchemaNode> EnumerateNodes(SchemaNode root)
    {
        var seen = new HashSet<SchemaNode>();
        var pending = new Stack<SchemaNode>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!seen.Add(node))
            {
                continue;
            }

            foreach (var property in node.Properties.Values)
            {
                pending.Push(property);
            }

            if (node.Items is not null)
            {
                pending.Push(node.Items);
            }

            if (node.AdditionalPropertiesSchema is not null)
            {
                pending.Push(node.AdditionalPropertiesSchema);
            }
        }

        return [.. seen];
    }
}
