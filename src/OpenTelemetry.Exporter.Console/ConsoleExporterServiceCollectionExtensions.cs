// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Shared registration for <see cref="ConsoleExporterOptions"/>, called from every
/// <c>AddConsoleExporter</c> registration path.
/// </summary>
internal static class ConsoleExporterServiceCollectionExtensions
{
    private const string ConsoleSectionPath = "OpenTelemetry:Console";

    /// <summary>
    /// Registers an <see cref="IOptionsFactory{TOptions}"/> that binds the
    /// <see cref="ConsoleExporterOptions"/> instance registered under the default name
    /// from the <c>OpenTelemetry:Console</c> configuration section when the options are
    /// created.
    /// </summary>
    /// <remarks>
    /// A value that cannot be converted throws, like any other options binding.
    /// The section is not watched, so changes need a restart.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the registration to.</param>
    /// <returns>The supplied <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddConsoleExporterServices(this IServiceCollection services)
    {
        services.RegisterOptionsFactory((_, configuration, name) =>
            {
                var options = new ConsoleExporterOptions();

                if (name == Options.DefaultName)
                {
                    configuration.GetSection(ConsoleSectionPath).Bind(options);
                }

                return options;
            });

        return services;
    }
}
