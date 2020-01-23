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
    public class LoggingSpan : TelemetrySpan
    {
        private Status status;
        public LoggingSpan(string name, SpanKind kind)
        {
            Logger.Log($"Span.ctor({name}, {kind})");
            this.Name = name;
            this.Kind = kind;
        }

        public string Name { get; set; }

        /// <inheritdoc/>
        public override SpanContext Context { get; }

        /// <inheritdoc/>
        public override Status Status { set => this.status = value; }

        public SpanKind? Kind { get; set; }

        /// <inheritdoc/>
        public override bool IsRecording => true;

        /// <inheritdoc/>
        public override void AddEvent(string name)
        {
            Logger.Log($"Span.AddEvent({name})");
        }

        /// <inheritdoc/>
        public override void AddEvent(Event newEvent)
        {
            Logger.Log($"Span.AddEvent({newEvent})");
        }

        /// <inheritdoc/>
        public override void End()
        {
            Logger.Log($"Span.End, Name: {this.Name}");
        }

        /// <inheritdoc/>
        public override void End(DateTimeOffset endTimestamp)
        {
            Logger.Log($"Span.End, Name: {this.Name}, Timestamp: {endTimestamp:o}");
        }

        public override void SetAttribute(string key, object value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, long value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, double value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public override void SetAttribute(string key, bool value)
        {
            this.LogSetAttribute(key, value);
        }

        /// <inheritdoc/>
        public override void UpdateName(string name)
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
