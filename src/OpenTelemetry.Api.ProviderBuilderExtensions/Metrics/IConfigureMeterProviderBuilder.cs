// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents something that configures the <see cref="MeterProviderBuilder"/> type.
/// </summary>
// Note: This API may be made public if there is a need for it.
internal interface IConfigureMeterProviderBuilder
{
    /// <summary>
    /// Invoked to configure a <see cref="MeterProviderBuilder"/> instance.
    /// </summary>
    /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    void ConfigureBuilder(IServiceProvider serviceProvider, MeterProviderBuilder meterProviderBuilder);
}
