// <copyright file="NoopSpan.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    public class TestSpan : ISpan
    {
        public SpanContext Context { get; }
        public bool IsRecording { get; }
        public Status Status { get; set; }
        public bool HasEnded { get; }
        public void UpdateName(string name)
        {
            throw new NotImplementedException();
        }

        public void SetAttribute(string key, object value)
        {
        }

        public void SetAttribute(string key, long value)
        {
        }

        public void SetAttribute(string key, bool value)
        {
        }

        public void SetAttribute(string key, double value)
        {
        }

        public void AddEvent(string name)
        {
        }

        public void AddEvent(Event newEvent)
        {
        }

        public void End()
        {
        }

        public void End(DateTimeOffset endTimestamp)
        {
        }
    }
}
