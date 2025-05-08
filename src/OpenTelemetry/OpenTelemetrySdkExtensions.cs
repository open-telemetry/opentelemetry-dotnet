// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Contains methods for extending the <see cref="OpenTelemetrySdk"/> class.
/// </summary>
public static class OpenTelemetrySdkExtensions
{
    /// <summary>
    /// Gets the <see cref="ILoggerFactory"/> contained in an <see
    /// cref="OpenTelemetrySdk"/> instance.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="ILoggerFactory"/> will be a no-op instance.
    /// Call <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithLogging(IOpenTelemetryBuilder)"/>
    /// to enable logging.
    /// </remarks>
    /// <param name="sdk"><see cref="OpenTelemetrySdk"/>.</param>
    /// <returns><see cref="ILoggerFactory"/>.</returns>
    public static ILoggerFactory GetLoggerFactory(this OpenTelemetrySdk sdk)
    {
        Guard.ThrowIfNull(sdk);

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        return (ILoggerFactory?)sdk.Services.GetService(typeof(ILoggerFactory))
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
            ?? NullLoggerFactory.Instance;
    }
}
