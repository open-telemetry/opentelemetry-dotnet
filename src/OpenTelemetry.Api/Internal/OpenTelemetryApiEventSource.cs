// <copyright file="OpenTelemetryApiEventSource.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// EventSource implementation for OpenTelemetry API.
    /// This is used for internal logging of this library.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Api")]
    internal class OpenTelemetryApiEventSource : EventSource
    {
        public static OpenTelemetryApiEventSource Log = new OpenTelemetryApiEventSource();

        [NonEvent]
        public void ActivityContextExtractException(string format, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.FailedToExtractActivityContext(format, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void BaggageExtractException(string format, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.FailedToExtractBaggage(format, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void TracestateExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.TracestateExtractError(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void TracestateKeyIsInvalid(ReadOnlySpan<char> key)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.TracestateKeyIsInvalid(key.ToString());
            }
        }

        [NonEvent]
        public void TracestateValueIsInvalid(ReadOnlySpan<char> value)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.TracestateValueIsInvalid(value.ToString());
            }
        }

        [Event(3, Message = "Failed to parse tracestate: too many items", Level = EventLevel.Warning)]
        public void TooManyItemsInTracestate()
        {
            this.WriteEvent(3);
        }

        [Event(4, Message = "Tracestate key is invalid, key = '{0}'", Level = EventLevel.Warning)]
        public void TracestateKeyIsInvalid(string key)
        {
            this.WriteEvent(4, key);
        }

        [Event(5, Message = "Tracestate value is invalid, value = '{0}'", Level = EventLevel.Warning)]
        public void TracestateValueIsInvalid(string value)
        {
            this.WriteEvent(5, value);
        }

        [Event(6, Message = "Tracestate parse error: '{0}'", Level = EventLevel.Warning)]
        public void TracestateExtractError(string error)
        {
            this.WriteEvent(6, error);
        }

        [Event(7, Message = "Calling method '{0}' with invalid argument '{1}', issue '{2}'.", Level = EventLevel.Warning)]
        public void InvalidArgument(string methodName, string argumentName, string issue)
        {
            this.WriteEvent(7, methodName, argumentName, issue);
        }

        [Event(8, Message = "Failed to extract activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToExtractActivityContext(string format, string exception)
        {
            this.WriteEvent(8, format, exception);
        }

        [Event(9, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToInjectActivityContext(string format, string error)
        {
            this.WriteEvent(9, format, error);
        }

        [Event(10, Message = "Failed to extract baggage in format: '{0}', baggage: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToExtractBaggage(string format, string exception)
        {
            this.WriteEvent(10, format, exception);
        }

        [Event(11, Message = "Failed to inject baggage in format: '{0}', baggage: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToInjectBaggage(string format, string error)
        {
            this.WriteEvent(11, format, error);
        }

        [NonEvent]
        public void LogCritical(Type type, Exception exception, string message, params object[] args)
        {
            if (this.IsEnabled(EventLevel.Critical, (EventKeywords)(-1)))
            {
                string formattedMessage = string.Format($"{type.FullName}: {message}", args);
                if (exception == null)
                {
                    this.EmitCriticalEvent(formattedMessage);
                }
                else
                {
                    this.EmitCriticalEventWithException(formattedMessage, exception.ToInvariantString());
                }
            }
        }

        [Event(12, Message = "{0}", Level = EventLevel.Critical)]
        public void EmitCriticalEvent(string message)
        {
            this.WriteEvent(12, message);
        }

        [Event(13, Message = "{0} Exception: {1}", Level = EventLevel.Critical)]
        public void EmitCriticalEventWithException(string message, string exception)
        {
            this.WriteEvent(13, message, exception);
        }

        [NonEvent]
        public void LogError(Type type, Exception exception, string message, params object[] args)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                string formattedMessage = string.Format($"{type.FullName}: {message}", args);
                if (exception == null)
                {
                    this.EmitErrorEvent(formattedMessage);
                }
                else
                {
                    this.EmitErrorEventWithException(formattedMessage, exception.ToInvariantString());
                }
            }
        }

        [Event(14, Message = "{0}", Level = EventLevel.Error)]
        public void EmitErrorEvent(string message)
        {
            this.WriteEvent(14, message);
        }

        [Event(15, Message = "{0} Exception: {1}", Level = EventLevel.Error)]
        public void EmitErrorEventWithException(string message, string exception)
        {
            this.WriteEvent(15, message, exception);
        }

        [NonEvent]
        public void LogWarning(Type type, Exception exception, string message, params object[] args)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                string formattedMessage = string.Format($"{type.FullName}: {message}", args);
                if (exception == null)
                {
                    this.EmitWarningEvent(formattedMessage);
                }
                else
                {
                    this.EmitWarningEventWithException(formattedMessage, exception.ToInvariantString());
                }
            }
        }

        [Event(16, Message = "{0}", Level = EventLevel.Warning)]
        public void EmitWarningEvent(string message)
        {
            this.WriteEvent(16, message);
        }

        [Event(17, Message = "{0} Exception: {1}", Level = EventLevel.Warning)]
        public void EmitWarningEventWithException(string message, string exception)
        {
            this.WriteEvent(17, message, exception);
        }

        [NonEvent]
        public void LogInformation(Type type, Exception exception, string message, params object[] args)
        {
            if (this.IsEnabled(EventLevel.Informational, (EventKeywords)(-1)))
            {
                string formattedMessage = string.Format($"{type.FullName}: {message}", args);
                if (exception == null)
                {
                    this.EmitInformationEvent(formattedMessage);
                }
                else
                {
                    this.EmitInformationEventWithException(formattedMessage, exception.ToInvariantString());
                }
            }
        }

        [Event(18, Message = "{0}", Level = EventLevel.Informational)]
        public void EmitInformationEvent(string message)
        {
            this.WriteEvent(18, message);
        }

        [Event(19, Message = "{0} Exception: {1}", Level = EventLevel.Informational)]
        public void EmitInformationEventWithException(string message, string error)
        {
            this.WriteEvent(19, message, error);
        }

        [NonEvent]
        public void LogVerbose(Type type, Exception exception, string message, params object[] args)
        {
            if (this.IsEnabled(EventLevel.Verbose, (EventKeywords)(-1)))
            {
                string formattedMessage = string.Format($"{type.FullName}: {message}", args);
                if (exception == null)
                {
                    this.EmitVerboseEvent(formattedMessage);
                }
                else
                {
                    this.EmitVerboseEventWithException(formattedMessage, exception.ToInvariantString());
                } 
            }
        }

        [Event(20, Message = "{0}", Level = EventLevel.Verbose)]
        public void EmitVerboseEvent(string message)
        {
            this.WriteEvent(20, message);
        }

        [Event(21, Message = "{0} Exception: {1}", Level = EventLevel.Verbose)]
        public void EmitVerboseEventWithException(string message, string error)
        {
            this.WriteEvent(21, message, error);
        }
    }
}
