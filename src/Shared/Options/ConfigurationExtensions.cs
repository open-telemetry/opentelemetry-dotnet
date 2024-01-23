// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
#if !NETFRAMEWORK && !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Internal;

internal static class ConfigurationExtensions
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

    public static IServiceCollection RegisterOptionsFactory<T>(
        this IServiceCollection services,
        Func<IConfiguration, T> optionsFactoryFunc)
        where T : class, new()
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(optionsFactoryFunc != null, "optionsFactoryFunc was null");

        services!.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new DelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc!(c),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<T>>(),
                sp.GetServices<IPostConfigureOptions<T>>(),
                sp.GetServices<IValidateOptions<T>>());
        });

        return services!;
    }

    public static IServiceCollection RegisterOptionsFactory<T>(
        this IServiceCollection services,
        Func<IServiceProvider, IConfiguration, string, T> optionsFactoryFunc)
        where T : class, new()
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(optionsFactoryFunc != null, "optionsFactoryFunc was null");

        services!.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new DelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc!(sp, c, n),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<T>>(),
                sp.GetServices<IPostConfigureOptions<T>>(),
                sp.GetServices<IValidateOptions<T>>());
        });

        return services!;
    }
}
