// <copyright file="SharedEventSource.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Shared
{
    [EventSource(Name = EventSourceName)]
    internal sealed class SharedEventSource : EventSource
    {
        public static SharedEventSource Log = new SharedEventSource();
        private const string EventSourceName = "OpenTelemetry-Shared";

        [NonEvent]
        public void Critical(string message, object value = null)
        {
            this.Write(EventLevel.Critical, message, value);
        }

        [NonEvent]
        public void Error(string message, object value = null)
        {
            this.Write(EventLevel.Error, message, value);
        }

        [NonEvent]
        public void Warning(string message, object value = null)
        {
            this.Write(EventLevel.Warning, message, value);
        }

        [NonEvent]
        public void Informational(string message, object value = null)
        {
            this.Write(EventLevel.Informational, message, value);
        }

        [NonEvent]
        public void Verbose(string message, object value = null)
        {
            this.Write(EventLevel.Verbose, message, value);
        }

        [Event(1, Message = "{0}", Level = EventLevel.Critical)]
        public void WriteCritical(string message) => this.WriteEvent(1, message);

        [Event(2, Message = "{0}", Level = EventLevel.Error)]
        public void WriteError(string message) => this.WriteEvent(2, message);

        [Event(3, Message = "{0}", Level = EventLevel.Warning)]
        public void WriteWarning(string message) => this.WriteEvent(3, message);

        [Event(4, Message = "{0}", Level = EventLevel.Informational)]
        public void WriteInformational(string message) => this.WriteEvent(4, message);

        [Event(5, Message = "{0}", Level = EventLevel.Verbose)]
        public void WriteVerbose(string message) => this.WriteEvent(5, message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetMessage(object value)
        {
            return value is Exception exception ? exception.ToInvariantString() : value.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(EventLevel eventLevel, string message, object value)
        {
            if (this.IsEnabled(eventLevel, (EventKeywords)(-1)))
            {
                var logMessage = value == null ? message : $"{message} - {GetMessage(value)}";

                switch (eventLevel)
                {
                    case EventLevel.Critical:
                        this.WriteCritical(logMessage);
                        break;
                    case EventLevel.Error:
                        this.WriteError(logMessage);
                        break;
                    case EventLevel.Informational:
                        this.WriteInformational(logMessage);
                        break;
                    case EventLevel.Verbose:
                        this.WriteVerbose(logMessage);
                        break;
                    case EventLevel.Warning:
                        this.WriteWarning(logMessage);
                        break;
                }
            }
        }
    }
}
