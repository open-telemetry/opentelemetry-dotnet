// <copyright file="OpenTelemetrySerilogSink.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetrySerilogSink : ILogEventSink, IDisposable
{
    private static readonly string[] LogEventLevels = new string[]
    {
        nameof(LogEventLevel.Verbose),
        nameof(LogEventLevel.Debug),
        nameof(LogEventLevel.Information),
        nameof(LogEventLevel.Warning),
        nameof(LogEventLevel.Error),
        nameof(LogEventLevel.Fatal),
    };

    private readonly LoggerProvider loggerProvider;
    private readonly bool includeRenderedMessage;
    private readonly Logger logger;
    private readonly bool disposeProvider;

    public OpenTelemetrySerilogSink(
        LoggerProvider loggerProvider,
        OpenTelemetrySerilogSinkOptions? options,
        bool disposeProvider)
    {
        Debug.Assert(loggerProvider != null, "loggerProvider was null");

        options ??= new();

        this.loggerProvider = loggerProvider!;
        this.disposeProvider = disposeProvider;

        this.logger = loggerProvider!.GetLogger(new LoggerOptions(
            new InstrumentationScope("OpenTelemetry.Extensions.Serilog")
            {
                Version = $"semver:{typeof(OpenTelemetrySerilogSink).Assembly.GetName().Version}",
            }));

        this.includeRenderedMessage = options.IncludeRenderedMessage;
    }

    public void Emit(LogEvent logEvent)
    {
        Debug.Assert(logEvent != null, "LogEvent was null.");

        LogRecordData data = new(Activity.Current)
        {
            Timestamp = logEvent!.Timestamp.UtcDateTime,
            Body = logEvent.MessageTemplate.Text,
        };

        uint severityNumber = (uint)logEvent.Level;
        if (severityNumber < 6)
        {
            data.SeverityText = LogEventLevels[severityNumber];
            data.Severity = (LogRecordSeverity)severityNumber;
        }

        LogRecordAttributeList attributes = default;

        if (this.includeRenderedMessage)
        {
            attributes.Add("serilog.rendered_message", logEvent.RenderMessage());
        }

        var exception = logEvent.Exception;
        if (exception != null)
        {
            attributes.RecordException(exception);
        }

        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
        {
            // TODO: Serilog supports complex type logging. This is not yet
            // supported in OpenTelemetry.
            if (property.Key == Constants.SourceContextPropertyName
                && property.Value is ScalarValue sourceContextValue)
            {
                attributes.Add("serilog.source_context", sourceContextValue.Value as string);
            }
            else if (property.Value is ScalarValue scalarValue)
            {
                attributes.Add(property.Key, scalarValue.Value);
            }
            else if (property.Value is SequenceValue sequenceValue)
            {
                IReadOnlyList<LogEventPropertyValue> elements = sequenceValue.Elements;
                if (elements.Count > 0)
                {
                    // Note: The goal here is to build a typed array (eg
                    // int[]) if all the element types match otherwise
                    // fallback to object[]

                    Type? elementType = null;
                    Array? values = null;

                    for (int i = 0; i < elements.Count; i++)
                    {
                        if (elements[i] is ScalarValue value)
                        {
                            Type currentElementType = value.Value?.GetType() ?? typeof(object);

                            if (values == null)
                            {
                                elementType = currentElementType;
                                values = Array.CreateInstance(elementType, elements.Count);
                            }
                            else if (!elementType!.IsAssignableFrom(currentElementType))
                            {
                                // Array with mixed types detected
                                object[] newValues = new object[elements.Count];
                                values.CopyTo(newValues, 0);
                                values = newValues;
                                elementType = typeof(object);
                            }

                            values.SetValue(value.Value, i);
                        }
                    }

                    if (values != null)
                    {
                        attributes.Add(property.Key, values);
                    }
                }
            }
        }

        this.logger.EmitLog(in data, in attributes);
    }

    public void Dispose()
    {
        if (this.disposeProvider)
        {
            this.loggerProvider.Dispose();
        }
    }
}
