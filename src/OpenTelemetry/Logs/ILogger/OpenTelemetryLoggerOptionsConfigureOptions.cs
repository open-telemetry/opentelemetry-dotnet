// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Logs;

/// <summary>
/// Binds the <c>Logging:OpenTelemetry</c> configuration section to <see cref="OpenTelemetryLoggerOptions"/>
/// using the configuration binding source generator instead of reflection.
/// </summary>
internal sealed class OpenTelemetryLoggerOptionsConfigureOptions : IConfigureOptions<OpenTelemetryLoggerOptions>
{
    private readonly IConfiguration configuration;

    public OpenTelemetryLoggerOptionsConfigureOptions(ILoggerProviderConfiguration<OpenTelemetryLoggerProvider> providerConfiguration)
    {
        this.configuration = providerConfiguration.Configuration;
    }

    public void Configure(OpenTelemetryLoggerOptions options) => this.configuration.Bind(options);
}
