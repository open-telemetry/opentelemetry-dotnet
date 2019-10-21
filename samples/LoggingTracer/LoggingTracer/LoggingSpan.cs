// <copyright file="LoggingSpan.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public class LoggingSpan : ISpan
    {
        public LoggingSpan(string name, SpanKind kind)
        {
            Logger.Log($"Span.ctor({name}, {kind})");
            this.Name = name;
            this.Kind = kind;
        }

        public string Name { get; set; }

        /// <inheritdoc/>
        public SpanContext Context { get; set; }

        /// <inheritdoc/>
        public Status Status { get; set; }

        public SpanKind? Kind { get; set; }

        public bool HasEnded { get; set; }

        /// <inheritdoc/>
        public bool IsRecording => true;

        /// <inheritdoc/>
        public void AddEvent(string name)
        {
            Logger.Log($"Span.AddEvent({name})");
        }

        /// <inheritdoc/>
        public void AddEvent(string name, IDictionary<string, object> attributes)
        {
            Logger.Log($"Span.AddEvent({name}, attributes: {attributes.Count})");
        }

        /// <inheritdoc/>
        public void AddEvent(Event newEvent)
        {
            Logger.Log($"Span.AddEvent({newEvent})");
        }

        /// <inheritdoc/>
        public void AddLink(Link link)
        {
            Logger.Log($"Span.AddLink({link})");
        }

        /// <inheritdoc/>
        public void End()
        {
            Logger.Log($"Span.End, Name: {this.Name}");
        }

        /// <inheritdoc/>
        public void End(DateTimeOffset endTimestamp)
        {
            Logger.Log($"Span.End, Name: {this.Name}, Timestamp: {endTimestamp:o}");
        }

        public void SetAttribute(string key, object value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, string value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, long value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, double value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public void SetAttribute(string key, bool value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public void SetAttribute(KeyValuePair<string, object> keyValuePair)
        {
            Logger.Log($"Span.SetAttributes(attributes: {keyValuePair})");
            this.SetAttribute(keyValuePair.Key, keyValuePair.Value);
        }

        /// <inheritdoc/>
        public void UpdateName(string name)
        {
            Logger.Log($"Span.UpdateName({name})");
            this.Name = name;
        }

        private void LogSetAttribute(string key, object value)
        {
            Logger.Log($"Span.SetAttribute({key}, {value})");
        }
    }
}
