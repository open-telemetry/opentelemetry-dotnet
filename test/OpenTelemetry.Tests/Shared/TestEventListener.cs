// <copyright file="TestEventListener.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Threading;

namespace OpenTelemetry.Tests
{
    /// <summary>
    /// Event listener for testing event sources.
    /// </summary>
    internal class TestEventListener : EventListener
    {
        /// <summary>A queue of events that have been logged.</summary>
        private readonly Queue<EventWrittenEventArgs> events;

        /// <summary>
        /// Lock for event writing tracking.
        /// </summary>
        private readonly AutoResetEvent eventWritten;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestEventListener"/> class.
        /// </summary>
        public TestEventListener()
        {
            this.events = new Queue<EventWrittenEventArgs>();
            this.eventWritten = new AutoResetEvent(false);
            this.OnOnEventWritten = e =>
            {
                this.events.Enqueue(e);
                this.eventWritten.Set();
            };
        }

        /// <summary>Gets or sets the handler for event source creation.</summary>
        public Action<EventSource> OnOnEventSourceCreated { get; set; }

        /// <summary>Gets or sets the handler for event source writes.</summary>
        public Action<EventWrittenEventArgs> OnOnEventWritten { get; set; }

        /// <summary>Gets the events that have been written.</summary>
        public IEnumerable<EventWrittenEventArgs> Messages
        {
            get
            {
                if (this.events.Count == 0)
                {
                    this.eventWritten.WaitOne(TimeSpan.FromSeconds(5));
                }

                while (this.events.Count != 0)
                {
                    yield return this.events.Dequeue();
                }
            }
        }

        /// <summary>
        /// Clears all event messages so that testing can assert expected counts.
        /// </summary>
        public void ClearMessages()
        {
            this.events.Clear();
        }

        /// <summary>Handler for event source writes.</summary>
        /// <param name="eventData">The event data that was written.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            this.OnOnEventWritten(eventData);
        }

        /// <summary>Handler for event source creation.</summary>
        /// <param name="eventSource">The event source that was created.</param>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Check for null because this method is called by the base class constror before we can initialize it
            Action<EventSource> callback = this.OnOnEventSourceCreated;
            if (callback != null)
            {
                callback(eventSource);
            }
        }
    }
}
