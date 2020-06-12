// <copyright file="TestSpan.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    public class TestSpan : TelemetrySpan
    {
        private Status status;

        public override SpanContext Context { get; }

        public override bool IsRecording { get; }

        public override Status Status { set => this.status = value; }

        public override void UpdateName(string name)
        {
            throw new NotImplementedException();
        }

        public override void SetAttribute(string key, object value)
        {
        }

        public override void SetAttribute(string key, long value)
        {
        }

        public override void SetAttribute(string key, bool value)
        {
        }

        public override void SetAttribute(string key, double value)
        {
        }

        public override void AddEvent(string name)
        {
        }

        public override void AddEvent(Event newEvent)
        {
        }

        public override void End()
        {
        }

        public override void End(DateTimeOffset endTimestamp)
        {
        }
    }
}
