// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains OpenTelemetry logging options.
/// </summary>
public class OpenTelemetryLoggerOptions
{
    internal readonly List<Func<IServiceProvider, BaseProcessor<LogRecord>>> ProcessorFactories = new();
    internal ResourceBuilder? ResourceBuilder;

    /// <summary>
    /// Gets or sets a value indicating whether or not formatted log message
    /// should be included on generated <see cref="LogRecord"/>s. Default
    /// value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Note: When set to <see langword="false"/> a formatted log message
    /// will not be included if a message template can be found. If a
    /// message template is not found, a formatted log message is always
    /// included.
    /// </remarks>
    public bool IncludeFormattedMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not log scopes should be
    /// included on generated <see cref="LogRecord"/>s. Default value:
    /// <see langword="false"/>.
    /// </summary>
    public bool IncludeScopes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not log state should be
    /// parsed into <see cref="LogRecord.Attributes"/> on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>As of OpenTelemetry v1.5 state parsing is handled automatically if
    /// the state logged implements <see cref="IReadOnlyList{T}"/> or <see
    /// cref="IEnumerable{T}"/> where <c>T</c> is <c>KeyValuePair&lt;string,
    /// object&gt;</c> than <see cref="LogRecord.Attributes"/> will be set
    /// regardless of the value of <see cref="ParseStateValues"/>.</item>
    /// <item>When <see cref="ParseStateValues"/> is set to <see
    /// langword="true"/> <see cref="LogRecord.State"/> will always be <see
    /// langword="null"/>. When <see cref="ParseStateValues"/> is set to <see
    /// langword="false"/> <see cref="LogRecord.State"/> will always be set to
    /// the logged state to support legacy exporters which access <see
    /// cref="LogRecord.State"/> directly. Exporters should NOT access <see
    /// cref="LogRecord.State"/> directly because is NOT safe and may lead to
    /// exceptions or incorrect data especially when using batching. Exporters
    /// should use <see cref="LogRecord.Attributes"/> to safely access any data
    /// attached to log messages.</item>
    /// </list>
    /// </remarks>
    public bool ParseStateValues { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not attributes specified
    /// via log state should be included on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="true"/>.
    /// </summary>
    internal bool IncludeAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether or not the <see
    /// cref="Activity.TraceStateString"/> for the current <see
    /// cref="Activity"/> should be included on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
    /// </summary>
    internal bool IncludeTraceState { get; set; }

    /// <summary>
    /// Adds processor to the options.
    /// </summary>
    /// <param name="processor">Log processor to add.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    public OpenTelemetryLoggerOptions AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Guard.ThrowIfNull(processor);

        this.ProcessorFactories.Add(_ => processor);

        return this;
    }

    /// <summary>
    /// Adds a processor to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    public OpenTelemetryLoggerOptions AddProcessor(
        Func<IServiceProvider, BaseProcessor<LogRecord>> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        this.ProcessorFactories.Add(implementationFactory);

        return this;
    }

    /// <summary>
    /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from. Overwrites currently set ResourceBuilder.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    public OpenTelemetryLoggerOptions SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);

        this.ResourceBuilder = resourceBuilder;
        return this;
    }

    internal OpenTelemetryLoggerOptions Copy()
    {
        return new()
        {
            IncludeFormattedMessage = this.IncludeFormattedMessage,
            IncludeScopes = this.IncludeScopes,
            ParseStateValues = this.ParseStateValues,
            IncludeAttributes = this.IncludeAttributes,
            IncludeTraceState = this.IncludeTraceState,
        };
    }
}