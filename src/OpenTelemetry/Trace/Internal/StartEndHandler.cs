// <copyright file="StartEndHandler.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace.Internal
{
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Export;

    internal sealed class StartEndHandler : IStartEndHandler
    {
        private readonly ISpanExporter spanExporter;
        private readonly IEventQueue eventQueue;

        public StartEndHandler(ISpanExporter spanExporter, IEventQueue eventQueue)
        {
            this.spanExporter = spanExporter;
            this.eventQueue = eventQueue;
        }

        public void OnEnd(ISpan span)
        {
            if (span.IsRecordingEvents)
            {
                this.eventQueue.Enqueue(new SpanEndEvent(span, this.spanExporter));
            }
        }

        public void OnStart(ISpan span)
        {
        }

        private sealed class SpanEndEvent : IEventQueueEntry
        {
            private readonly ISpan span;
            private readonly ISpanExporter spanExporter;

            public SpanEndEvent(
                    ISpan span,
                    ISpanExporter spanExporter)
            {
                this.span = span;
                this.spanExporter = spanExporter;
            }

            public void Process()
            {
                if (this.span.IsRecordingEvents)
                {
                    this.spanExporter.AddSpan(this.span);
                }
            }
        }
    }
}
