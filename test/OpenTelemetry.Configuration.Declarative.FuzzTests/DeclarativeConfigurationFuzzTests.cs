// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// AddOpenTelemetryDeclarativeConfiguration and DeclarativeConfigurationException are experimental.
// Suppress once here rather than at every call/catch site.
#pragma warning disable OTEL1006

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public class DeclarativeConfigurationFuzzTests
{
    private const int MaxTests = 200;

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
}
