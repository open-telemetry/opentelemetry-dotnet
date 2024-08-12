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
    private static readonly NullLoggerFactory NoopLoggerFactory = new();

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

        return (ILoggerFactory?)sdk.Services.GetService(typeof(ILoggerFactory))
            ?? NoopLoggerFactory;
    }
}
