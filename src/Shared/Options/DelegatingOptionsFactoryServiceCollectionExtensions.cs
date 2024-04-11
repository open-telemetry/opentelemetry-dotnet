// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

internal static class DelegatingOptionsFactoryServiceCollectionExtensions
{
#if NET6_0_OR_GREATER
    public static IServiceCollection RegisterOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
    public static IServiceCollection RegisterOptionsFactory<T>(
#endif
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

#if NET6_0_OR_GREATER
    public static IServiceCollection RegisterOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
    public static IServiceCollection RegisterOptionsFactory<T>(
#endif
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

#if NET6_0_OR_GREATER
    public static IServiceCollection DisableOptionsReloading<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
    public static IServiceCollection DisableOptionsReloading<T>(
#endif
        this IServiceCollection services)
        where T : class
    {
        Debug.Assert(services != null, "services was null");

        services!.TryAddSingleton<IOptionsMonitor<T>>(sp
            => throw new NotSupportedException($"IOptionsMonitor is not supported with the '{typeof(T)}' options type."));
        services!.TryAddSingleton<IOptionsSnapshot<T>>(sp
            => throw new NotSupportedException($"IOptionsSnapshot is not supported with the '{typeof(T)}' options type."));

        return services!;
    }
}
