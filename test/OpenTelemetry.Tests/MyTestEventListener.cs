// <copyright file="MyTestEventListener.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;

    public class MyTestEventListener : EventListener
    {
        public List<EventWrittenEventArgs> CapturedEvents = new();

        private readonly List<EventSource> subscribedEventSources = new();
        private readonly Guid currentThreadActivityId = Guid.NewGuid();
        private readonly string eventName;
        private readonly EventLevel eventLevel;

        public MyTestEventListener(string eventName, EventLevel eventLevel = EventLevel.Verbose)
        {
            EventSource.SetCurrentThreadActivityId(this.currentThreadActivityId);
            this.eventName = eventName;
            this.eventLevel = eventLevel;
        }

        public override void Dispose()
        {
            foreach (EventSource eventSource in this.subscribedEventSources)
            {
                this.DisableEvents(eventSource);
            }

            base.Dispose();
            GC.SuppressFinalize(this);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource?.Name == this.eventName)
            {
                this.subscribedEventSources.Add(eventSource);
                this.EnableEvents(eventSource, this.eventLevel, EventKeywords.All);
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.ActivityId == this.currentThreadActivityId)
            {
                this.CapturedEvents.Add(eventData);
            }
        }
    }
}
