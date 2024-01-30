// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables SqlClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
#if NET6_0_OR_GREATER
    [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif
    public static TracerProviderBuilder AddSqlClientInstrumentation(this TracerProviderBuilder builder)
        => AddSqlClientInstrumentation(builder, name: null, configureSqlClientInstrumentationOptions: null);

    /// <summary>
    /// Enables SqlClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configureSqlClientInstrumentationOptions">Callback action for configuring <see cref="SqlClientInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
#if NET6_0_OR_GREATER
    [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif
    public static TracerProviderBuilder AddSqlClientInstrumentation(
        this TracerProviderBuilder builder,
        Action<SqlClientInstrumentationOptions> configureSqlClientInstrumentationOptions)
        => AddSqlClientInstrumentation(builder, name: null, configureSqlClientInstrumentationOptions);

    /// <summary>
    /// Enables SqlClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureSqlClientInstrumentationOptions">Callback action for configuring <see cref="SqlClientInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
#if NET6_0_OR_GREATER
    [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif

    public static TracerProviderBuilder AddSqlClientInstrumentation(
        this TracerProviderBuilder builder,
        string name,
        Action<SqlClientInstrumentationOptions> configureSqlClientInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configureSqlClientInstrumentationOptions != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureSqlClientInstrumentationOptions));
        }

        builder.AddInstrumentation(sp =>
        {
            var sqlOptions = sp.GetRequiredService<IOptionsMonitor<SqlClientInstrumentationOptions>>().Get(name);

            return new SqlClientInstrumentation(sqlOptions);
        });

        builder.AddSource(SqlActivitySourceHelper.ActivitySourceName);

        return builder;
    }
}
