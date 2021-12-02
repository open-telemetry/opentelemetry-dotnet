using System;
using System.Net.Http;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public static class HttpClientFactoryExporterOptionsBuilderExtensions
    {
        public static TBuilder ConfigureHttpClientFactory<TOptions, TBuilder>(
            this IHttpClientFactoryExporterOptionsBuilder<TOptions, TBuilder> builder, Action<IHttpClientFactoryExporterOptions> configure)
            where TOptions : class, IHttpClientFactoryExporterOptions, new()
            where TBuilder : ExporterOptionsBuilder<TOptions, TBuilder>
        {
            Guard.Null(configure);

            builder.BuilderInstance.Configure((sp, o) => configure(o));

            return builder.BuilderInstance;
        }

        public static TBuilder ConfigureHttpClientFactory<TOptions, TBuilder>(
            this IHttpClientFactoryExporterOptionsBuilder<TOptions, TBuilder> builder, Action<IServiceProvider, IHttpClientFactoryExporterOptions> configure)
            where TOptions : class, IHttpClientFactoryExporterOptions, new()
            where TBuilder : ExporterOptionsBuilder<TOptions, TBuilder>
        {
            Guard.Null(configure);

            builder.BuilderInstance.Configure(configure);

            return builder.BuilderInstance;
        }

        public static bool TryEnableIHttpClientFactoryIntegration(
            this IHttpClientFactoryExporterOptions options,
            IServiceProvider serviceProvider,
            string httpClientName,
            Action<HttpClient> configure = null)
        {
            Type httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
            if (httpClientFactoryType != null)
            {
                object httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
                if (httpClientFactory != null)
                {
                    MethodInfo createClientMethod = httpClientFactoryType.GetMethod(
                        "CreateClient",
                        BindingFlags.Public | BindingFlags.Instance,
                        binder: null,
                        new Type[] { typeof(string) },
                        modifiers: null);
                    if (createClientMethod != null)
                    {
                        options.HttpClientFactory = () =>
                        {
                            HttpClient client = (HttpClient)createClientMethod.Invoke(httpClientFactory, new object[] { httpClientName });

                            configure?.Invoke(client);

                            return client;
                        };

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
