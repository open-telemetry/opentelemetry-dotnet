// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Configuration.Declarative.Tests;

internal static class RepresentativeDeclarativeConfigurationConsumerExtensions
{
    // Stands in for how a real typed consumer acquires the document: resolve the
    // accessor at construction time, tolerate its absence, and contribute nothing when absent.
    internal static IServiceCollection AddRepresentativeDeclarativeConfigurationConsumer(
        this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            new RepresentativeDeclarativeConfigurationConsumer(
                DeclarativeConfigurationDocumentAccessorResolver.Find(sp)));
        return services;
    }
}
