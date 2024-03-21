// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if !NETFRAMEWORK && !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;

namespace Microsoft.Extensions.Configuration;

internal static class OpenTelemetryConfigurationExtensions
{
    public static Action<string, string>? LogInvalidEnvironmentVariable = null;

    public delegate bool TryParseFunc<T>(
        string value,
#if !NETFRAMEWORK && !NETSTANDARD2_0
        [NotNullWhen(true)]
#endif
        out T? parsedValue);

    public static bool TryGetStringValue(
        this IConfiguration configuration,
        string key,
#if !NETFRAMEWORK && !NETSTANDARD2_0
        [NotNullWhen(true)]
#endif
        out string? value)
    {
        value = configuration[key] is string configValue ? configValue : null;

        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryGetUriValue(
        this IConfiguration configuration,
        string key,
#if !NETFRAMEWORK && !NETSTANDARD2_0
        [NotNullWhen(true)]
#endif
        out Uri? value)
    {
        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = null;
            return false;
        }

        if (!Uri.TryCreate(stringValue, UriKind.Absolute, out value))
        {
            LogInvalidEnvironmentVariable?.Invoke(key, stringValue!);
            return false;
        }

        return true;
    }

    public static bool TryGetIntValue(
        this IConfiguration configuration,
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
            LogInvalidEnvironmentVariable?.Invoke(key, stringValue!);
            return false;
        }

        return true;
    }

    public static bool TryGetBoolValue(
        this IConfiguration configuration,
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
            LogInvalidEnvironmentVariable?.Invoke(key, stringValue!);
            return false;
        }

        return true;
    }

    public static bool TryGetValue<T>(
        this IConfiguration configuration,
        string key,
        TryParseFunc<T> tryParseFunc,
#if !NETFRAMEWORK && !NETSTANDARD2_0
        [NotNullWhen(true)]
#endif
        out T? value)
    {
        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!tryParseFunc(stringValue!, out value))
        {
            LogInvalidEnvironmentVariable?.Invoke(key, stringValue!);
            return false;
        }

        return true;
    }
}
