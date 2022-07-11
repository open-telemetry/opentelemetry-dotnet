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
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace OpenTelemetry.Logs
{
    internal sealed class OpenTelemetrySerilogSink : ILogEventSink, IDisposable
    {
        private readonly OpenTelemetryLoggerProvider openTelemetryLoggerProvider;
        private readonly bool includeFormattedMessage;
        private readonly LogEmitter logEmitter;
        private readonly bool disposeProvider;

        public OpenTelemetrySerilogSink(OpenTelemetryLoggerProvider openTelemetryLoggerProvider, bool disposeProvider)
        {
            Debug.Assert(openTelemetryLoggerProvider != null);

            this.openTelemetryLoggerProvider = openTelemetryLoggerProvider!;
            this.disposeProvider = disposeProvider;

            var logEmitter = this.openTelemetryLoggerProvider.CreateEmitter();
            Debug.Assert(logEmitter != null);

            this.logEmitter = logEmitter!;

            // TODO: This project can only access IncludeFormattedMessage
            // because it can see SDK internals. At some point this is likely
            // not to be the case. Need to figure out where to put
            // IncludeFormattedMessage so that extensions can see it. Ideas:
            // Make it public on OpenTelemetryLoggerProvider or expose it on
            // LogEmitter instance.
            this.includeFormattedMessage = this.openTelemetryLoggerProvider.IncludeFormattedMessage;
        }

        public void Emit(LogEvent logEvent)
        {
            Debug.Assert(logEvent != null, "LogEvent was null.");

            LogRecordData data = new(Activity.Current)
            {
                Timestamp = logEvent!.Timestamp.UtcDateTime,
                LogLevel = (LogLevel)(int)logEvent.Level,
                Message = this.includeFormattedMessage ? logEvent.RenderMessage() : logEvent.MessageTemplate.Text,
                Exception = logEvent.Exception,
            };

            LogRecordAttributeList attributes = default;
            foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
            {
                // TODO: Serilog supports complex type logging. This is not yet
                // supported in OpenTelemetry.
                if (property.Key == Constants.SourceContextPropertyName
                    && property.Value is ScalarValue sourceContextValue)
                {
                    data.CategoryName = sourceContextValue.Value as string;
                }
                else if (property.Value is ScalarValue scalarValue)
                {
                    attributes.Add(property.Key, scalarValue.Value);
                }
                else if (property.Value is SequenceValue sequenceValue)
                {
                    IReadOnlyList<LogEventPropertyValue> elements = sequenceValue.Elements;
                    if (elements.Count > 0 && elements[0] is ScalarValue)
                    {
                        object[] values = new object[elements.Count];
                        for (int i = 0; i < elements.Count; i++)
                        {
                            if (elements[i] is ScalarValue value)
                            {
                                values[i] = value.Value;
                            }
                        }

                        attributes.Add(property.Key, values);
                    }
                }
            }

            this.logEmitter.Log(in data, in attributes);
        }

        public void Dispose()
        {
            if (this.disposeProvider)
            {
                this.openTelemetryLoggerProvider.Dispose();
            }
        }
    }
}
