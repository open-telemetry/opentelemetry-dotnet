// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

/// <summary>
/// Represents something that configures the <see cref="LoggerProviderBuilder"/> type.
/// </summary>
// Note: This API may be made public if there is a need for it.
internal interface IConfigureLoggerProviderBuilder
{
    /// <summary>
    /// Invoked to configure a <see cref="LoggerProviderBuilder"/> instance.
    /// </summary>
    /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    void ConfigureBuilder(IServiceProvider serviceProvider, LoggerProviderBuilder loggerProviderBuilder);
}