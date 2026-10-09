// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public class SchemaPropertyNameValidatorFuzzTests
{
    private const int MaxTests = 200;
    private const int MaxDepth = 3;

    // Mostly real schema keys, so documents reach deep into the sampler definitions, plus one
    // undefined key so the generated trees both load and fail.
    private static readonly string[] Vocabulary =
    [
        "always_on",
        "always_off",
        "parent_based",
        "root",
        "remote_parent_sampled",
        "trace_id_ratio_based",
        "ratio",
        "jaeger_remote/development",
        "initial_sampler",
        "typo",
    ];

    private static readonly Arbitrary<SharedSamplerCase> SharedSamplerArbitrary = Arb.From(GenerateCase(), Shrink);

    // A mapping reachable through an anchor and alias is validated from two places, one of which
    // can be experimental. The outcome must not depend on which place the document spells first,
    // and must match the document with the alias expanded: the same load result and, on failure,
    // the same first rejected property path.
    [Property(MaxTest = MaxTests)]
    public Property AliasedSamplerHasTheSameOutcomeAsTheExpandedDocument() =>
        Prop.ForAll(
            SharedSamplerArbitrary,
            testCase =>
            {
                var aliased = Load(testCase.Render(useAlias: true));
                var expanded = Load(testCase.Render(useAlias: false));

                return (aliased == expanded)
                    .Classify(expanded is null, "loads")
                    .Classify(expanded is not null, "rejected");
            });

    // Guards the generator: both outcomes must be reachable, otherwise the property above
    // could pass vacuously.
    [Fact]
    public void GeneratorProducesLoadingAndRejectedDocuments()
    {
        var cases = Gen.Sample(SharedSamplerArbitrary.Generator, 200, 0).ToArray();
        var outcomes = cases.Select(c => Load(c.Render(useAlias: false)) is null).ToArray();

        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }

    // Returns null when the document loads, otherwise the first rejected property path.
    private static string? Load(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(path, yaml);
            _ = DeclarativeConfigurationReader.Read(new FilePath(path));
            return null;
        }
        catch (DeclarativeConfigurationException ex)
        {
            const string prefix = "Property '";
            var end = ex.Message.IndexOf("' (line", StringComparison.Ordinal);
            return ex.Message.StartsWith(prefix, StringComparison.Ordinal) && end > 0
                ? ex.Message.Substring(prefix.Length, end - prefix.Length)
                : ex.Message;
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static IEnumerable<SharedSamplerCase> Shrink(SharedSamplerCase testCase) =>
        ShrinkTree(testCase.Shared).Select(tree => testCase with { Shared = tree });

    private static IEnumerable<Tree> ShrinkTree(Tree tree)
    {
        for (var i = 0; i < tree.Keys.Length; i++)
        {
            yield return new Tree(
                [.. tree.Keys.Where((_, j) => j != i)],
                [.. tree.Children.Where((_, j) => j != i)]);

            foreach (var child in ShrinkTree(tree.Children[i]))
            {
                var children = (Tree[])tree.Children.Clone();
                children[i] = child;
                yield return new Tree(tree.Keys, children);
            }
        }
    }

    private static Gen<Tree> GenerateTree(int depth)
    {
        if (depth == 0)
        {
            return Gen.Constant(new Tree([], []));
        }

        return
            from count in Gen.Choose(0, 3)
            from shuffled in Gen.Shuffle(Vocabulary)
            let keys = shuffled.Take(count).ToArray()
            from children in Gen.ArrayOf(GenerateTree(depth - 1), keys.Length)
            select new Tree(keys, children);
    }

    private static Gen<SharedSamplerCase> GenerateCase() =>
        from shared in GenerateTree(MaxDepth)
        from experimentalFirst in Gen.Elements(true, false)
        select new SharedSamplerCase(shared, experimentalFirst);

    private sealed record Tree(string[] Keys, Tree[] Children)
    {
        internal string Render() =>
            "{" + string.Join(", ", this.Keys.Select((key, i) => $"{key}: {this.Children[i].Render()}")) + "}";
    }

    private sealed record SharedSamplerCase(Tree Shared, bool ExperimentalFirst)
    {
        public override string ToString() => this.Render(useAlias: true);

        internal string Render(bool useAlias)
        {
            var body = this.Shared.Render();
            var first = useAlias ? "&shared " + body : body;
            var second = useAlias ? "*shared" : body;

            var stable = "parent_based: {root: ";
            var experimental = "jaeger_remote/development: {initial_sampler: ";

            var sampler = this.ExperimentalFirst
                ? $"{experimental}{first}}}, {stable}{second}}}"
                : $"{stable}{first}}}, {experimental}{second}}}";

            return $"file_format: \"1.2\"\ntracer_provider: {{sampler: {{{sampler}}}}}\n";
        }
    }
}
