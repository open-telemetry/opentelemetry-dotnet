// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Core;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public class DeclarativeConfigurationFuzzTests
{
    private const int MaxTests = 200;
    private const int MaxDepth = 4;

    private static readonly string[] Scalars =
    [
        "text",
        "true",
        "42",
        "0x1F",
        "~",
        "1e999",
        "${VAR}",
        "${VAR:-fallback}",
        "$$literal",
        "!!int 7",
        "!!str 9",
        "!!int oops",
    ];

    private static readonly Gen<YamlValue> ScalarOrAliasGenerator = Gen.Frequency(
        (4, Gen.Elements(Scalars).Select(value => (YamlValue)new ScalarValue(value))),
        (1, Gen.Choose(0, 15).Select(index => (YamlValue)new AliasValue(index))));

    private static readonly Gen<YamlDocument> AnchoredYamlGenerator = Gen.Sized(size =>
        from sectionCount in Gen.Choose(1, 3)
        from sections in Gen.ArrayOf(GenerateYamlValue(Math.Min(size, MaxDepth), depth: 0), sectionCount)
        select new YamlDocument(sections));

    private static readonly Arbitrary<YamlDocument> AnchoredYamlArbitrary =
        Arb.From(AnchoredYamlGenerator, ShrinkYamlDocument);

    [Property(MaxTest = MaxTests)]
    public Property LoadArbitraryYamlNeverThrowsUnexpectedException() =>
        Prop.ForAll(
            Gen.Sized(size =>
                Gen.ArrayOf(
                    Gen.Choose(0, 127).Select(c => (char)c),
                    Math.Min(size + 1, 512))
                .Select(chars => new string(chars))).ToArbitrary(),
            yamlContent =>
            {
                // The public API only accepts a file path; there is no stream overload, so a temp file is required.
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
                try
                {
                    File.WriteAllText(tempPath, yamlContent);
                    try
                    {
                        var builder = new ConfigurationBuilder();
                        builder.AddOpenTelemetryDeclarativeConfiguration(tempPath);
                        _ = builder.Build();
                    }
                    catch (DeclarativeConfigurationException)
                    {
                        // Expected: malformed YAML, unsupported file_format, invalid substitution, etc.
                    }
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            });

    // Random ASCII almost never produces an anchor, so this generates structurally plausible YAML
    // that is rich in anchors and aliases, including self-referential ones. The claim under test is
    // totality: reading such a document always terminates, and any failure is a configuration or
    // YAML error rather than an arbitrary exception escaping the parser.
    [Property(MaxTest = MaxTests)]
    public Property LoadAnchoredYamlAlwaysTerminatesWithAKnownOutcome() =>
        Prop.ForAll(
            AnchoredYamlArbitrary,
            document =>
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
                try
                {
                    File.WriteAllText(tempPath, document.Render());
                    try
                    {
                        _ = DeclarativeConfigurationReader.Read(
                            new FilePath(tempPath),
                            name => name.Length % 2 == 0 ? "value" : null);
                    }
                    catch (DeclarativeConfigurationException)
                    {
                        // Expected: alias cycle, merge key, invalid tag, invalid substitution, schema violation.
                    }
                    catch (YamlException)
                    {
                        // Expected: the generated text is not valid YAML.
                    }
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            });

    // A generator that quietly stopped emitting cycles would leave the property above passing while
    // testing less than it was written for. That risk is covered by the explicit cycle tests in
    // DeclarativeConfigurationDocumentPropertiesTests and DeclarativeConfigurationReaderTests, which
    // pin detection directly rather than by sampling; this file owns only the totality claim.
    private static Gen<YamlValue> GenerateYamlValue(int size, int depth)
    {
        if (size == 0 || depth >= MaxDepth)
        {
            return ScalarOrAliasGenerator;
        }

        var childGenerator = GenerateYamlValue(size - 1, depth + 1);
        var mappingGenerator =
            from hasAnchor in Gen.Elements(true, false)
            from usesMergeKey in Gen.Elements(true, false, false)
            from childCount in Gen.Choose(1, 2)
            from children in Gen.ArrayOf(childGenerator, childCount)
            select (YamlValue)new MappingValue(hasAnchor, children, usesMergeKey);
        var sequenceGenerator =
            from hasAnchor in Gen.Elements(true, false)
            from childCount in Gen.Choose(1, 2)
            from children in Gen.ArrayOf(childGenerator, childCount)
            select (YamlValue)new SequenceValue(hasAnchor, children);

        return Gen.Frequency(
            (3, ScalarOrAliasGenerator),
            (2, mappingGenerator),
            (2, sequenceGenerator));
    }

    // Range/index syntax is unavailable on net462, which this project targets.
    private static T[] AllButLast<T>(T[] items)
    {
        var result = new T[items.Length - 1];
        Array.Copy(items, result, result.Length);
        return result;
    }

    private static IEnumerable<YamlDocument> ShrinkYamlDocument(YamlDocument document)
    {
        if (document.Sections.Length > 1)
        {
            yield return new YamlDocument(AllButLast(document.Sections));
        }

        for (var i = 0; i < document.Sections.Length; i++)
        {
            foreach (var shrunkValue in ShrinkYamlValue(document.Sections[i]))
            {
                var sections = (YamlValue[])document.Sections.Clone();
                sections[i] = shrunkValue;
                yield return new YamlDocument(sections);
            }
        }
    }

    private static IEnumerable<YamlValue> ShrinkYamlValue(YamlValue value)
    {
        switch (value)
        {
            case AliasValue { AnchorIndex: > 0 }:
                yield return new AliasValue(0);
                break;
            case ScalarValue { Value: not "text" }:
                yield return new ScalarValue("text");
                break;
            case CollectionValue collection:
                if (collection is MappingValue { UsesMergeKey: true } merging)
                {
                    yield return new MappingValue(merging.HasAnchor, merging.Children, UsesMergeKey: false);
                }

                if (collection.HasAnchor)
                {
                    yield return collection.With(hasAnchor: false, collection.Children);
                }

                if (collection.Children.Length > 1)
                {
                    yield return collection.With(collection.HasAnchor, AllButLast(collection.Children));
                }

                foreach (var child in collection.Children)
                {
                    yield return child;
                }

                for (var i = 0; i < collection.Children.Length; i++)
                {
                    foreach (var shrunkChild in ShrinkYamlValue(collection.Children[i]))
                    {
                        var children = (YamlValue[])collection.Children.Clone();
                        children[i] = shrunkChild;
                        yield return collection.With(collection.HasAnchor, children);
                    }
                }

                break;
        }
    }

    // The caller has written a mapping key or sequence marker with no line break; this appends the
    // value and the break.
    private static void AppendValue(StringBuilder builder, RenderState state, YamlValue value, int indent)
    {
        switch (value)
        {
            case ScalarValue scalar:
                builder.Append(' ').AppendLine(scalar.Value);
                return;
            case AliasValue alias:
                builder.Append(' ').AppendLine(state.GetAlias(alias.AnchorIndex) ?? "text");
                return;
            case CollectionValue collection:
                if (collection.HasAnchor)
                {
                    builder.Append(" &").Append(state.DeclareAnchor());
                }

                builder.AppendLine();
                var pad = new string(' ', indent * 2);
                for (var i = 0; i < collection.Children.Length; i++)
                {
                    if (collection is MappingValue mapping)
                    {
                        if (mapping.UsesMergeKey && i == 0)
                        {
                            builder.Append(pad).Append("<<:");
                        }
                        else
                        {
                            builder.Append(pad).Append('k').Append(i).Append(':');
                        }
                    }
                    else
                    {
                        builder.Append(pad).Append('-');
                    }

                    AppendValue(builder, state, collection.Children[i], indent + 1);
                }

                return;
        }
    }

    private abstract record YamlValue;

    private sealed record ScalarValue(string Value) : YamlValue;

    private sealed record AliasValue(int AnchorIndex) : YamlValue;

    private abstract record CollectionValue(bool HasAnchor, YamlValue[] Children) : YamlValue
    {
        internal abstract CollectionValue With(bool hasAnchor, YamlValue[] children);
    }

    // A mapping's first key is rendered as `<<` when UsesMergeKey is set, so the generator also
    // covers rejecting YAML 1.1 merge syntax at arbitrary depths and through aliases.
    private sealed record MappingValue(bool HasAnchor, YamlValue[] Children, bool UsesMergeKey = false)
        : CollectionValue(HasAnchor, Children)
    {
        internal override CollectionValue With(bool hasAnchor, YamlValue[] children) =>
            new MappingValue(hasAnchor, children, this.UsesMergeKey);
    }

    private sealed record SequenceValue(bool HasAnchor, YamlValue[] Children) : CollectionValue(HasAnchor, Children)
    {
        internal override CollectionValue With(bool hasAnchor, YamlValue[] children) =>
            new SequenceValue(hasAnchor, children);
    }

    private sealed record YamlDocument(YamlValue[] Sections)
    {
        public override string ToString() => this.Render();

        internal string Render()
        {
            var state = new RenderState();
            var builder = new StringBuilder();
            builder.AppendLine("""file_format: "1.0" """.TrimEnd());

            for (var i = 0; i < this.Sections.Length; i++)
            {
                builder.Append("section").Append(i).Append(':');
                AppendValue(builder, state, this.Sections[i], indent: 1);
            }

            return builder.ToString();
        }
    }

    private sealed class RenderState
    {
        private readonly List<string> anchors = [];

        internal string DeclareAnchor()
        {
            var name = $"a{this.anchors.Count}";
            this.anchors.Add(name);
            return name;
        }

        internal string? GetAlias(int index) =>
            this.anchors.Count == 0 ? null : $"*{this.anchors[index % this.anchors.Count]}";
    }
}
