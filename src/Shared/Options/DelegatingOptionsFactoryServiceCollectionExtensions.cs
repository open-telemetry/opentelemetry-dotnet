// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

internal static class DelegatingOptionsFactoryServiceCollectionExtensions
{
#if NET
    public static IServiceCollection RegisterOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
    public static IServiceCollection RegisterOptionsFactory<T>(
#endif
        this IServiceCollection services,
        Func<IConfiguration, T> optionsFactoryFunc)
        where T : class
    {
        services.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new DelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc(c),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<T>>(),
                sp.GetServices<IPostConfigureOptions<T>>(),
                sp.GetServices<IValidateOptions<T>>());
        });

        return services;
    }

#if NET
    public static IServiceCollection RegisterOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
    public static IServiceCollection RegisterOptionsFactory<T>(
#endif
        this IServiceCollection services,
        Func<IServiceProvider, IConfiguration, string, T> optionsFactoryFunc)
        where T : class
    {
        services.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new DelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc(sp, c, n),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IConfigureOptions<T>>(),
                sp.GetServices<IPostConfigureOptions<T>>(),
                sp.GetServices<IValidateOptions<T>>());
        });

        return services;
    }

#if NET
    public static IServiceCollection DisableOptionsReloading<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
    public static IServiceCollection DisableOptionsReloading<T>(
#endif
        this IServiceCollection services)
        where T : class
    {
        services.TryAddSingleton<IOptionsMonitor<T>, SingletonOptionsManager<T>>();
        services.TryAddScoped<IOptionsSnapshot<T>, SingletonOptionsManager<T>>();

        return services;
    }
}
