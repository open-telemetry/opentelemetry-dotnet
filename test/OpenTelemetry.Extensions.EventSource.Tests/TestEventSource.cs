// <copyright file="TestEventSource.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Extensions.EventSource.Tests
{
    [EventSource(Name = TestEventSource.EventSourceName)]
    public sealed class TestEventSource : System.Diagnostics.Tracing.EventSource
    {
        public const string EventSourceName = "OpenTelemetry.Extensions.EventSource.Tests";

        public const int SimpleEventId = 1;
        public const string SimpleEventMessage = "Warning event with no arguments.";

        public const int ComplexEventId = 2;
        public const string ComplexEventMessage = "Information event with two arguments: '{0}' & '{1}'.";
        public const string ComplexEventMessageStructured = "Information event with two arguments: '{arg1}' & '{arg2}'.";

        public static TestEventSource Log { get; } = new();

        [Event(SimpleEventId, Message = SimpleEventMessage, Level = EventLevel.Warning)]
        public void SimpleEvent()
        {
            this.WriteEvent(SimpleEventId);
        }

        [Event(ComplexEventId, Message = ComplexEventMessage, Level = EventLevel.Informational)]
        public void ComplexEvent(string arg1, int arg2)
        {
            this.WriteEvent(ComplexEventId, arg1, arg2);
        }

        [Event(3, Level = EventLevel.Verbose)]
        public void WorkStart()
        {
            this.WriteEvent(3);
        }

        [Event(4, Level = EventLevel.Verbose)]
        public void WorkStop()
        {
            this.WriteEvent(4);
        }

        [Event(5, Level = EventLevel.Verbose)]
        public void SubworkStart()
        {
            this.WriteEvent(5);
        }

        [Event(6, Level = EventLevel.Verbose)]
        public void SubworkStop()
        {
            this.WriteEvent(6);
        }
    }
}
