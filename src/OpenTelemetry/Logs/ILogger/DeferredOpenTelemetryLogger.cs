// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs;

internal sealed class DeferredOpenTelemetryLogger : ILogger
{
    private readonly string categoryName;
    private readonly OpenTelemetryLoggerProvider loggerProvider;
    private readonly Lock syncObject = new();
    private LoggerState? loggerState;

    public DeferredOpenTelemetryLogger(
        OpenTelemetryLoggerProvider loggerProvider,
        string categoryName)
    {
        this.loggerProvider = loggerProvider;
        this.categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => this.loggerProvider.ScopeProvider?.Push(state) ?? NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None && !Sdk.SuppressInstrumentation;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        this.GetLogger()?.Log(logLevel, eventId, state, exception, formatter);
    }

    private OpenTelemetryLogger? GetLogger()
    {
        if (this.loggerProvider.Provider is not LoggerProviderSdk loggerProviderSdk)
        {
            return null;
        }

        var loggerState = Volatile.Read(ref this.loggerState);
        if (loggerState == null || !ReferenceEquals(loggerState.Provider, loggerProviderSdk))
        {
            lock (this.syncObject)
            {
                loggerState = this.loggerState;
                if (loggerState == null || !ReferenceEquals(loggerState.Provider, loggerProviderSdk))
                {
                    loggerState = new(
                        loggerProviderSdk,
                        new OpenTelemetryLogger(loggerProviderSdk, this.loggerProvider.Options, this.categoryName));
                    Volatile.Write(ref this.loggerState, loggerState);
                }
            }
        }

        loggerState.Logger.ScopeProvider = this.loggerProvider.ScopeProvider;
        return loggerState.Logger;
    }

    private sealed class LoggerState(LoggerProviderSdk provider, OpenTelemetryLogger logger)
    {
        public OpenTelemetryLogger Logger { get; } = logger;

        public LoggerProviderSdk Provider { get; } = provider;
    }
}
