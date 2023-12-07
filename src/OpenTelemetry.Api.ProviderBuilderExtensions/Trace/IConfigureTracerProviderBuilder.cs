// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Represents something that configures the <see cref="TracerProviderBuilder"/> type.
/// </summary>
// Note: This API may be made public if there is a need for it.
internal interface IConfigureTracerProviderBuilder
{
    /// <summary>
    /// Invoked to configure a <see cref="TracerProviderBuilder"/> instance.
    /// </summary>
    /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder);
}
