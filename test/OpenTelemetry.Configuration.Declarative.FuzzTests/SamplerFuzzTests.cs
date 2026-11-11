// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public sealed class SamplerFuzzTests : IDisposable
{
    private const int MaxTests = 200;
    private const int MaxDepth = 4;

    private static readonly string[] LeafNames =
    [
        "always_on",
        "always_off",
        "trace_id_ratio_based",
    ];

    private static readonly string[] ChildKeys =
    [
        "root",
        "remote_parent_sampled",
        "remote_parent_not_sampled",
        "local_parent_sampled",
        "local_parent_not_sampled",
    ];

    private static readonly string[] Ratios =
    [
        "0",
        "1",
        "0.5",
        "0.000001",
        "~",
    ];

    private readonly TemporaryDirectory directory = new();

    public void Dispose() => this.directory.Dispose();

    [Property(MaxTest = MaxTests)]
    public Property ValidSamplerTreeBuildsAndShutsDown() =>
        Prop.ForAll(
            Gen.Sized(size => GenerateSampler(Math.Min(size, MaxDepth), validOnly: true)).ToArbitrary(),
            sampler =>
            {
                var built = this.CreateSampler(sampler);

                Assert.True(built.Shutdown(0), $"Shutdown failed for {sampler}");
            });

    // Malformed trees may be rejected, but unexpected exceptions must not escape.
    [Property(MaxTest = MaxTests)]
    public Property SamplerTreeEitherFailsWithAConfigurationErrorOrBuilds() =>
        Prop.ForAll(
            Gen.Sized(size => GenerateSampler(Math.Min(size, MaxDepth), validOnly: false)).ToArbitrary(),
            sampler =>
            {
                Sampler built;
                try
                {
                    built = this.CreateSampler(sampler);
                }
                catch (DeclarativeConfigurationException)
                {
                    return;
                }

                Assert.True(built.Shutdown(0), $"Shutdown failed for {sampler}");
            });

    private static Gen<string> GenerateSampler(int depth, bool validOnly)
    {
        var named = Gen.Elements(validOnly ? LeafNames : [.. LeafNames, "composite/development", "x_unknown"])
            .Select(name => "{" + name + ": }");
        var withRatio = Gen.Elements(validOnly ? Ratios : [.. Ratios, "-1", "2", "1e999", ".nan", ".inf", "text", "true", "[]"])
            .Select(ratio => "{trace_id_ratio_based: {ratio: " + ratio + "}}");
        var leaf = Gen.OneOf(named, withRatio);

        if (depth == 0)
        {
            return leaf;
        }

        var child = GenerateSampler(depth - 1, validOnly);
        var value = validOnly ? child : Gen.Frequency((8, child), (1, Gen.Constant("~")));
        var childKeys = validOnly ? ChildKeys : [.. ChildKeys, "not_a_child"];

        // Distinct keys, because a repeated key is a YAML error rather than a sampler error.
        var children =
            from keys in Gen.Shuffle(childKeys)
            from count in Gen.Choose(0, childKeys.Length)
            from values in Gen.ArrayOf(value, count)
            select string.Join(", ", keys.Take(count).Zip(values, (key, entry) => key + ": " + entry));

        var parentBased = children.Select(entries => "{parent_based: {" + entries + "}}");
        var alwaysRecord = value.Select(root => "{always_record: {root: " + root + "}}");
        if (!validOnly)
        {
            alwaysRecord = Gen.OneOf(alwaysRecord, Gen.Constant("{always_record: {}}"));
        }

        return Gen.Frequency((3, leaf), (2, parentBased), (2, alwaysRecord));
    }

    private Sampler CreateSampler(string sampler)
    {
        var path = Path.Combine(this.directory.Path, $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(
            path,
            $$$"""
            file_format: "1.2"
            tracer_provider:
              processors:
                - simple: {exporter: {console: }}
              sampler: {{{sampler}}}

            """);

        var section = TracerProviderConfigurationBinder.Bind(DeclarativeConfigurationReader.Read(new FilePath(path)).Properties);
        Assert.NotNull(section);
        Assert.NotNull(section.Sampler);

        var serviceCollection = new ServiceCollection();
        BuiltInComponentProviders.Register(serviceCollection);
        using var services = serviceCollection.BuildServiceProvider();
        return SamplerFactory.Create(section.Sampler, services);
    }
}
