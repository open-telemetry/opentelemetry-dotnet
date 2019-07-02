﻿// <copyright file="BlankSpan.cs" company="OpenTelemetry Authors">
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
    using System;
    using System.Collections.Generic;

    internal sealed class BlankSpan : SpanBase
    {
        public static readonly BlankSpan Instance = new BlankSpan();

        private BlankSpan()
            : base(SpanContext.Blank, default(SpanOptions))
        {
        }

        public override string Name { get; protected set; }

        public override Status Status { get; set; }

        public override DateTimeOffset EndTime
        {
            get
            {
                return DateTimeOffset.MinValue;
            }
        }

        public override TimeSpan Latency
        {
            get
            {
                return TimeSpan.Zero;
            }
        }

        public override bool IsSampleToLocalSpanStore
        {
            get
            {
                return false;
            }
        }

        public override SpanId ParentSpanId
        {
            get
            {
                return null;
            }
        }

        public override bool HasEnded => true;

        public override bool IsRecordingEvents => false;

        public override void SetAttribute(string key, IAttributeValue value)
        {
        }

        public override void AddEvent(string name, IDictionary<string, IAttributeValue> attributes)
        {
        }

        public override void AddEvent(IEvent addEvent)
        {
        }

        public override void AddLink(ILink link)
        {
        }

        public override void End(EndSpanOptions options)
        {
        }

        public override string ToString()
        {
            return "BlankSpan";
        }

        public override SpanData ToSpanData()
        {
            return null;
        }

        public override void SetAttribute(string key, string value)
        {
        }

        public override void SetAttribute(string key, long value)
        {
        }

        public override void SetAttribute(string key, double value)
        {
        }

        public override void SetAttribute(string key, bool value)
        {
        }
    }
}
