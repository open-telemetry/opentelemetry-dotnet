// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Contains provider extension methods.
/// </summary>
public static class ProviderExtensions
{
    /// <summary>
    /// Gets the <see cref="Resource"/> associated with the <see cref="BaseProvider"/>.
    /// </summary>
    /// <param name="baseProvider"><see cref="BaseProvider"/>.</param>
    /// <returns><see cref="Resource"/>if found otherwise <see cref="Resource.Empty"/>.</returns>
    public static Resource GetResource(this BaseProvider baseProvider)
    {
        if (baseProvider is TracerProviderSdk tracerProviderSdk)
        {
            return tracerProviderSdk.Resource;
        }
        else if (baseProvider is MeterProviderSdk meterProviderSdk)
        {
            return meterProviderSdk.Resource;
        }
        else if (baseProvider is LoggerProviderSdk loggerProviderSdk)
        {
            return loggerProviderSdk.Resource;
        }
        else if (baseProvider is OpenTelemetryLoggerProvider openTelemetryLoggerProvider)
        {
            return openTelemetryLoggerProvider.Provider.GetResource();
        }

        return Resource.Empty;
    }

    /// <summary>
    /// Gets the <see cref="Resource"/> associated with the <see cref="BaseProvider"/>.
    /// </summary>
    /// <param name="baseProvider"><see cref="BaseProvider"/>.</param>
    /// <returns><see cref="Resource"/>if found otherwise <see cref="Resource.Empty"/>.</returns>
    public static Resource GetDefaultResource(this BaseProvider baseProvider)
    {
        var builder = ResourceBuilder.CreateDefault();
        builder.ServiceProvider = GetServiceProvider(baseProvider);
        return builder.Build();
    }

    internal static IServiceProvider? GetServiceProvider(this BaseProvider baseProvider)
    {
        if (baseProvider is TracerProviderSdk tracerProviderSdk)
        {
            return tracerProviderSdk.ServiceProvider;
        }
        else if (baseProvider is MeterProviderSdk meterProviderSdk)
        {
            return meterProviderSdk.ServiceProvider;
        }
        else if (baseProvider is LoggerProviderSdk loggerProviderSdk)
        {
            return loggerProviderSdk.ServiceProvider;
        }
        else if (baseProvider is OpenTelemetryLoggerProvider openTelemetryLoggerProvider)
        {
            return openTelemetryLoggerProvider.Provider.GetServiceProvider();
        }

        return null;
    }
}
