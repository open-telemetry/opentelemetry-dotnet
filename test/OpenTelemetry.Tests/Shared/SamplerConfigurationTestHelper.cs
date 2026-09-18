// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests;

internal static class SamplerConfigurationTestHelper
{
    /// <summary>
    /// Supplies <c>OTEL_TRACES_SAMPLER</c> and <c>OTEL_TRACES_SAMPLER_ARG</c> to
    /// <paramref name="builder"/> using an in-memory <see cref="IConfiguration"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="samplerConfigValue">Value for <c>OTEL_TRACES_SAMPLER</c>.</param>
    /// <param name="samplerArgConfigValue">Value for <c>OTEL_TRACES_SAMPLER_ARG</c>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddSamplerConfiguration(
        this TracerProviderBuilder builder,
        string? samplerConfigValue,
        string? samplerArgConfigValue = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [TracerProviderSdk.TracesSamplerConfigKey] = samplerConfigValue,
                [TracerProviderSdk.TracesSamplerArgConfigKey] = samplerArgConfigValue,
            })
            .Build();

        return builder.ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration));
    }
}
