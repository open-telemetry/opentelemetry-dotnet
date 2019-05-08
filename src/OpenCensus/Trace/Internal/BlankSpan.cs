// <copyright file="BlankSpan.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Internal
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Trace.Export;

    internal sealed class BlankSpan : SpanBase
    {
        public static readonly BlankSpan Instance = new BlankSpan();

        private BlankSpan()
            : base(SpanContext.Invalid, default(SpanOptions))
        {
        }

        /// <inheritdoc/>
        public override string Name { get; set; }

        public override Status Status { get; set; }

        public override SpanKind? Kind { get; set; }

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

        public override ISpanId ParentSpanId
        {
            get
            {
                return null;
            }
        }

        public override bool HasEnded => true;

        public override void PutAttributes(IDictionary<string, IAttributeValue> attributes)
        {
        }

        public override void PutAttribute(string key, IAttributeValue value)
        {
        }

        public override void AddAnnotation(string description, IDictionary<string, IAttributeValue> attributes)
        {
        }

        public override void AddAnnotation(IAnnotation annotation)
        {
        }

        public override void AddMessageEvent(IMessageEvent messageEvent)
        {
        }

        public override void AddLink(ILink link)
        {
        }

        public override void End(EndSpanOptions options)
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "BlankSpan";
        }

        public override ISpanData ToSpanData()
        {
            throw new NotImplementedException();
        }
    }
}
