﻿// <copyright file="SpanMock.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    /// <summary>
    /// A mock ISpan implementation for unit tests. Sometimes an actual Mock is just easier to deal with than objects created with Moq.
    /// </summary>
    internal class SpanMock : Trace.ISpan
    {
        private static readonly ReadOnlyDictionary<string, object> EmptyAttributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        public SpanMock(Trace.SpanContext spanContext)
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

        public SpanContext Context { get; private set; }

        public bool IsRecording { get; private set; }

        public Status Status { get; set; }

        public bool HasEnded { get; private set; }

        public void AddEvent(string name)
        {
            this.Events.Add(new Event(name));
        }

        public void AddEvent(string name, IDictionary<string, object> attributes)
        {
            this.Events.Add(new Event(name, default, attributes));
        }

        public void AddEvent(Event newEvent)
        {
            this.Events.Add(newEvent);
        }

        public void AddLink(Link link)
        {
            this.Links.Add(link);
        }

        public void End()
        {
            this.HasEnded = true;
        }

        public void End(DateTimeOffset endTimestamp)
        {
            this.End();
        }

        public void SetAttribute(string key, string value)
        {
            this.SetAttribute<string>(key, value);
        }

        public void SetAttribute(string key, long value)
        {
            this.SetAttribute<long>(key, value);
        }

        public void SetAttribute(string key, double value)
        {
            this.SetAttribute<double>(key, value);
        }

        public void SetAttribute(string key, bool value)
        {
            this.SetAttribute<bool>(key, value);
        }

        public void SetAttribute(KeyValuePair<string, object> keyValuePair)
        {
            this.Attributes.Add(keyValuePair);
        }

        public void UpdateName(string name)
        {
            this.Name = name;
        }

        private void SetAttribute<TValue>(string key, TValue value)
        {
            this.SetAttribute(new KeyValuePair<string, object>(key, value));
        }
    }
}
