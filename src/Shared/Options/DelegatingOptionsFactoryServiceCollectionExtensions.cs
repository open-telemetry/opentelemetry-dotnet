// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

internal static class DelegatingOptionsFactoryServiceCollectionExtensions
{
    public static IServiceCollection RegisterOptionsFactory<T>(
        this IServiceCollection services,
        Func<IConfiguration, T> optionsFactoryFunc)
        where T : class
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
        where T : class
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

    public static IServiceCollection RegisterSingletonOptionsFactory<T>(
        this IServiceCollection services,
        Func<IConfiguration, T> optionsFactoryFunc,
        Action<T> optionsResetAction)
        where T : class
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(optionsFactoryFunc != null, "optionsFactoryFunc was null");
        Debug.Assert(optionsResetAction != null, "optionsResetAction was null");

        services!.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new SingletonDelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc!(c),
                (n, o) => optionsResetAction!(o),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<T>>(),
                sp.GetServices<IPostConfigureOptions<T>>(),
                sp.GetServices<IValidateOptions<T>>());
        });

        return services!;
    }
}
