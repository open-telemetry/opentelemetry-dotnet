// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Logs;

namespace DedicatedLogging;

internal static class DedicatedLoggingServiceCollectionExtensions
{
    public static IServiceCollection AddDedicatedLogging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LoggerProviderBuilder> configureOpenTelemetry)
    {
        ArgumentNullException.ThrowIfNull(configureOpenTelemetry);

        services.TryAddSingleton(_ =>
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration);

                builder.AddOpenTelemetry();
            });

            services.ConfigureOpenTelemetryLoggerProvider(configureOpenTelemetry);

            var sp = services.BuildServiceProvider();

            return new DedicatedLoggerFactory(sp);
        });

        services.TryAdd(ServiceDescriptor.Singleton(typeof(IDedicatedLogger<>), typeof(DedicatedLogger<>)));

        return services;
    }

    private sealed class DedicatedLogger<T> : IDedicatedLogger<T>
    {
        private readonly ILogger innerLogger;

        public DedicatedLogger(DedicatedLoggerFactory loggerFactory)
        {
            this.innerLogger = loggerFactory.CreateLogger(typeof(T).FullName!);
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => this.innerLogger.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel)
            => this.innerLogger.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => this.innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }

    private sealed class DedicatedLoggerFactory : ILoggerFactory
    {
        private readonly ServiceProvider serviceProvider;
        private readonly ILoggerFactory innerLoggerFactory;

        public DedicatedLoggerFactory(ServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.innerLoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        }

        public void AddProvider(ILoggerProvider provider)
            => this.innerLoggerFactory.AddProvider(provider);

        public ILogger CreateLogger(string categoryName)
            => this.innerLoggerFactory.CreateLogger(categoryName);

        public void Dispose()
            => this.serviceProvider.Dispose();
    }
}
