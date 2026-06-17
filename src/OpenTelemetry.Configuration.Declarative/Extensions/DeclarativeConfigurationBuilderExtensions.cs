// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Configuration.Declarative;
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.Configuration;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Extension methods for adding the OpenTelemetry declarative configuration source to
/// an <see cref="IConfigurationBuilder"/>.
/// </summary>
public
#else
internal
#endif
    static class DeclarativeConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds the declarative YAML source, reading the file path from <c>OTEL_CONFIG_FILE</c>.
    /// </summary>
    /// <remarks>
    /// Appends the source after existing ones (YAML overrides earlier sources; sources added
    /// later override YAML). No-op when <c>OTEL_CONFIG_FILE</c> is unset, empty, or whitespace,
    /// or when the same file is already registered. See
    /// <see href="https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/diagnostics/experimental-apis/OTEL1006.md">OTEL1006</see>
    /// for integration guidance.
    /// </remarks>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <returns>The original <see cref="IConfigurationBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    [Experimental(DiagnosticDefinitions.DeclarativeConfigurationExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public static IConfigurationBuilder AddOpenTelemetryDeclarativeConfiguration(this IConfigurationBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        var filePath = Environment.GetEnvironmentVariable(OtelEnvironmentVariables.ConfigFile);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.OtelConfigFileNotSet();
            return builder;
        }

        return builder.AddOpenTelemetryDeclarativeConfiguration(filePath);
    }

    /// <summary>
    /// Adds the declarative YAML source using the specified file path.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOpenTelemetryDeclarativeConfiguration(IConfigurationBuilder)" path="/remarks"/></remarks>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <returns>The original <see cref="IConfigurationBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is null, empty, or whitespace.</exception>
    [Experimental(DiagnosticDefinitions.DeclarativeConfigurationExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public static IConfigurationBuilder AddOpenTelemetryDeclarativeConfiguration(
        this IConfigurationBuilder builder,
        string filePath)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddOpenTelemetryDeclarativeConfiguration(new FilePath(filePath));
    }

    // Internal overload so the path is never round-tripped through a string and re-resolved
    // by Path.GetFullPath. The FilePath struct stores the absolute path at construction time,
    // so callers that already hold a FilePath (e.g. overlay registration) can pass it directly.
    internal static IConfigurationBuilder AddOpenTelemetryDeclarativeConfiguration(
        this IConfigurationBuilder builder,
        FilePath path)
    {
        Guard.ThrowIfNull(builder);

        if (builder.Sources.OfType<DeclarativeConfigurationSource>().Any(s => s.FilePath == path))
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.SourceAlreadyRegisteredInBuilder(path.ToString());
            return builder;
        }

        builder.Sources.Add(new DeclarativeConfigurationSource(path));
        OpenTelemetryDeclarativeConfigurationEventSource.Log.SourceRegistered(path.ToString());
        return builder;
    }
}
