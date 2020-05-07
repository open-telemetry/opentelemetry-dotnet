// <copyright file="SpanMock.cs" company="OpenTelemetry Authors">
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
using System.Collections.ObjectModel;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    /// <summary>
    /// A mock ISpan implementation for unit tests. Sometimes an actual Mock is just easier to deal with than objects created with Moq.
    /// </summary>
    internal class SpanMock : TelemetrySpan, IDisposable
    {
        private Status status;

        public SpanMock(SpanContext spanContext)
        {
            this.Context = spanContext;
            this.Events = new List<Event>();
            this.Links = new List<Link>();
            this.Attributes = new List<KeyValuePair<string, object>>();
        }

        public string Name { get; private set; }

        public List<Event> Events { get; }

        public List<Link> Links { get; }

        public List<KeyValuePair<string, object>> Attributes { get; }

        public override SpanContext Context { get; }

        public override bool IsRecording { get; }

        public override Status Status { set => this.status = value; }

        public Status GetStatus()
        {
            return this.status;
        }

        public override void AddEvent(string name)
        {
            this.Events.Add(new Event(name));
        }

        public override void AddEvent(Event newEvent)
        {
            this.Events.Add(newEvent);
        }

        public override void End()
        {
        }

        public override void End(DateTimeOffset endTimestamp)
        {
            this.End();
        }

        public override void SetAttribute(string key, object value)
        {
            this.Attributes.Add(new KeyValuePair<string, object>(key, value));
        }

        public override void SetAttribute(string key, long value)
        {
            this.SetAttribute(key, (object)value);
        }

        public override void SetAttribute(string key, bool value)
        {
            this.SetAttribute(key, (object)value);
        }

        public override void SetAttribute(string key, double value)
        {
            this.SetAttribute(key, (object)value);
        }

        public override void UpdateName(string name)
        {
            this.Name = name;
        }

        public void Dispose()
        {
        }
    }
}
