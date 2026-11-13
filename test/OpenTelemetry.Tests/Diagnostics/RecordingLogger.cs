// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// An <see cref="ILogger"/> that records calls for diagnostics tests.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> entries = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => [.. this.entries];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= this.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => this.entries.Enqueue((logLevel, formatter(state, exception), exception));
}
