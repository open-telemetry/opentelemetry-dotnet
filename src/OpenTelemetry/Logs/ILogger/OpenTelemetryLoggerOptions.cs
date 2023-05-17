// <copyright file="OpenTelemetryLoggerOptions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains OpenTelemetry logging options.
/// </summary>
public class OpenTelemetryLoggerOptions
{
    private readonly LoggerProviderBuilder? loggerProviderBuilder;
    private readonly LoggerProviderServiceCollectionBuilder? serviceCollectionBuilder;
    private readonly LoggerProviderBuilderSdk? loggerProviderBuilderSdk;

    private bool includeFormattedMessage;
    private bool includeScopes;
    private bool parseStateValues;
    private bool includeAttributes = true;
    private bool includeTraceState;

    public OpenTelemetryLoggerOptions()
    {
        this.loggerProviderBuilder = Sdk.CreateLoggerProviderBuilder();
    }

    internal OpenTelemetryLoggerOptions(LoggerProviderServiceCollectionBuilder loggerProviderServiceCollection)
    {
        this.serviceCollectionBuilder = loggerProviderServiceCollection;
    }

    internal OpenTelemetryLoggerOptions(LoggerProviderBuilderSdk loggerProviderBuilderSdk)
    {
        this.loggerProviderBuilderSdk = loggerProviderBuilderSdk;
    }

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
    public bool IncludeFormattedMessage
    {
        get => this.GetFeature(o => o.includeFormattedMessage);
        set => this.SetFeature(o => o.includeFormattedMessage = value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether or not log scopes should be
    /// included on generated <see cref="LogRecord"/>s. Default value:
    /// <see langword="false"/>.
    /// </summary>
    public bool IncludeScopes
    {
        get => this.GetFeature(o => o.includeScopes);
        set => this.SetFeature(o => o.includeScopes = value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether or not log state should be
    /// parsed into <see cref="LogRecord.Attributes"/> on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>Parsing is only executed when the state logged does NOT
    /// implement <see cref="IReadOnlyList{T}"/> or <see
    /// cref="IEnumerable{T}"/> where <c>T</c> is <c>KeyValuePair&lt;string,
    /// object&gt;</c>.</item>
    /// <item>When <see cref="ParseStateValues"/> is set to <see
    /// langword="true"/> <see cref="LogRecord.State"/> will always be <see
    /// langword="null"/>.</item>
    /// </list>
    /// </remarks>
    public bool ParseStateValues
    {
        get => this.GetFeature(o => o.parseStateValues);
        set => this.SetFeature(o => o.parseStateValues = value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether or not attributes specified
    /// via log state should be included on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="true"/>.
    /// </summary>
    internal bool IncludeAttributes
    {
        get => this.GetFeature(o => o.includeAttributes);
        set => this.SetFeature(o => o.includeAttributes = value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether or not the <see
    /// cref="Activity.TraceStateString"/> for the current <see
    /// cref="Activity"/> should be included on generated <see
    /// cref="LogRecord"/>s. Default value: <see langword="false"/>.
    /// </summary>
    internal bool IncludeTraceState
    {
        get => this.GetFeature(o => o.includeTraceState);
        set => this.SetFeature(o => o.includeTraceState = value);
    }

    /// <summary>
    /// Adds processor to the options.
    /// </summary>
    /// <param name="processor">Log processor to add.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    // todo: [Obsolete("Call ConfigureOpenTelemetry instead AddProcessor will be removed in a future version.")]
    public OpenTelemetryLoggerOptions AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Guard.ThrowIfNull(processor);

        this.ConfigureOpenTelemetry(builder => builder.AddProcessor(processor));

        return this;
    }

    /// <summary>
    /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from. Overwrites currently set ResourceBuilder.
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
    /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
    // todo: [Obsolete("Call ConfigureOpenTelemetry instead SetResourceBuilder will be removed in a future version.")]
    public OpenTelemetryLoggerOptions SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);

        this.ConfigureOpenTelemetry(builder => builder.SetResourceBuilder(resourceBuilder));

        return this;
    }

    internal void ConfigureOpenTelemetry(Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        if (this.serviceCollectionBuilder != null)
        {
            // Used during AddOpenTelemetry lifetime. IServiceCollection is open, IServiceProvider is unavailable.
            configure(this.serviceCollectionBuilder);
        }
        else if (this.loggerProviderBuilderSdk != null)
        {
            // Used during OpenTelemetryLoggerProvider ctor lifetime. IServiceCollection is closed, IServiceProvider is available.
            configure(this.loggerProviderBuilderSdk);
        }
        else if (this.loggerProviderBuilder != null)
        {
            // Only used for new OpenTelemetryLoggerOptions(). Shouldn't really be done but it is possible in the shipped APIs.
            configure(this.loggerProviderBuilder);
        }
        else
        {
            throw new NotSupportedException("ConfigureOpenTelemetry is not supported on manually created OpenTelemetryLoggerOptions instances.");
        }
    }

    internal LoggerProvider Build()
    {
        if (this.loggerProviderBuilder != null)
        {
            return this.loggerProviderBuilder.Build();
        }

        throw new NotSupportedException("Build is only supported on manually created OpenTelemetryLoggerOptions instances.");
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

    private void SetFeature(Action<OpenTelemetryLoggerOptions> action)
    {
        if (this.serviceCollectionBuilder != null)
        {
            this.serviceCollectionBuilder.ConfigureServices(services =>
                services.Configure<OpenTelemetryLoggerOptions>(o => action(o)));
        }
        else
        {
            action(this);
        }
    }

    private bool GetFeature(Func<OpenTelemetryLoggerOptions, bool> func)
    {
        if (this.serviceCollectionBuilder != null)
        {
            throw new NotSupportedException("OpenTelemetryLoggerOptions cannot be read during AddOpenTelemetry invocation.");
        }

        return func(this);
    }
}
