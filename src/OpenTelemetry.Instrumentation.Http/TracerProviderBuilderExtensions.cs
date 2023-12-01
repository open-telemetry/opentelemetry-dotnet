// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of HttpClient instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables HttpClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddHttpClientInstrumentation(this TracerProviderBuilder builder)
        => AddHttpClientInstrumentation(builder, name: null, configureHttpClientTraceInstrumentationOptions: null);

    /// <summary>
    /// Enables HttpClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configureHttpClientTraceInstrumentationOptions">Callback action for configuring <see cref="HttpClientTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddHttpClientInstrumentation(
        this TracerProviderBuilder builder,
        Action<HttpClientTraceInstrumentationOptions> configureHttpClientTraceInstrumentationOptions)
        => AddHttpClientInstrumentation(builder, name: null, configureHttpClientTraceInstrumentationOptions);

    /// <summary>
    /// Enables HttpClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureHttpClientTraceInstrumentationOptions">Callback action for configuring <see cref="HttpClientTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddHttpClientInstrumentation(
        this TracerProviderBuilder builder,
        string name,
        Action<HttpClientTraceInstrumentationOptions> configureHttpClientTraceInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        // Note: Warm-up the status code and method mapping.
        _ = TelemetryHelper.BoxedStatusCodes;
        _ = RequestMethodHelper.KnownMethods;

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (configureHttpClientTraceInstrumentationOptions != null)
            {
                services.Configure(name, configureHttpClientTraceInstrumentationOptions);
            }
        });

#if NETFRAMEWORK
        builder.AddSource(HttpWebRequestActivitySource.ActivitySourceName);

        if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            deferredTracerProviderBuilder.Configure((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<HttpClientTraceInstrumentationOptions>>().Get(name);

                HttpWebRequestActivitySource.TracingOptions = options;
            });
        }
#else
        AddHttpClientInstrumentationSource(builder);

        builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<HttpClientTraceInstrumentationOptions>>().Get(name);

            return new HttpClientInstrumentation(options);
        });
#endif
        return builder;
    }

#if !NETFRAMEWORK
    internal static void AddHttpClientInstrumentationSource(
        this TracerProviderBuilder builder)
    {
        if (HttpHandlerDiagnosticListener.IsNet7OrGreater)
        {
            builder.AddSource(HttpHandlerDiagnosticListener.HttpClientActivitySourceName);
        }
        else
        {
            builder.AddSource(HttpHandlerDiagnosticListener.ActivitySourceName);
            builder.AddLegacySource("System.Net.Http.HttpRequestOut");
        }
    }
#endif
}
