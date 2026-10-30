// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;

namespace Microsoft.Extensions.Configuration;

internal static class OpenTelemetryConfigurationExtensions
{
    public delegate bool TryParseFunc<T>(
        string value,
        [NotNullWhen(true)]
        out T? parsedValue);

    public static string? GetStringValue(this IConfiguration configuration, string key)
    {
        try
        {
            return configuration[key];
        }
        catch (SecurityException)
        {
            // A configuration provider may defer reading a restricted environment variable.
            return null;
        }
    }

    public static bool TryGetStringValue(
        this IConfiguration configuration,
        string key,
        [NotNullWhen(true)]
        out string? value)
    {
        value = configuration.GetStringValue(key);
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryGetUriValue(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        [NotNullWhen(true)]
        out Uri? value)
    {
        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = null;
            return false;
        }

        if (!Uri.TryCreate(stringValue, UriKind.Absolute, out value))
        {
            // A URI may carry credentials in its userinfo, path, or query and one
            // which cannot be parsed cannot be reduced to the parts safe to log.
            logger.LogInvalidConfigurationValue(key, "[REDACTED]");
            return false;
        }

        return true;
    }

    public static bool TryGetIntValue(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        out int value)
    {
        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!int.TryParse(stringValue, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            logger.LogInvalidConfigurationValue(key, stringValue);
            return false;
        }

        return true;
    }

    public static bool TryGetBoolValue(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        out bool value)
    {
        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!bool.TryParse(stringValue, out value))
        {
            logger.LogInvalidConfigurationValue(key, stringValue);
            return false;
        }

        return true;
    }

    public static bool TryGetValue<T>(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        TryParseFunc<T> tryParseFunc,
        [NotNullWhen(true)]
        out T? value)
    {
        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!tryParseFunc(stringValue, out value))
        {
            logger.LogInvalidConfigurationValue(key, stringValue);
            return false;
        }

        return true;
    }
}
