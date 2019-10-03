// <copyright file="B3FormatTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Trace;
    using Xunit;
    using Xunit.Abstractions;

    public class B3FormatTest
    {
        private static readonly string TraceIdBase16 = "ff000000000000000000000000000041";
        private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateFromString(TraceIdBase16.AsSpan());
        private static readonly string TraceIdBase16EightBytes = "0000000000000041";
        private static readonly ActivityTraceId TraceIdEightBytes = ActivityTraceId.CreateFromString(("0000000000000000" + TraceIdBase16EightBytes).AsSpan());
        private static readonly string SpanIdBase16 = "ff00000000000041";
        private static readonly ActivitySpanId SpanId = ActivitySpanId.CreateFromString(SpanIdBase16.AsSpan());

        private static readonly ActivityTraceFlags TraceOptions = ActivityTraceFlags.Recorded;
        private readonly B3Format b3Format = new B3Format();


        private static readonly Action<IDictionary<string, string>, string, string> setter = (d, k, v) => d[k] = v;
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> getter = (d, k) => { d.TryGetValue(k, out var v); return new string[] { v }; };
        readonly ITestOutputHelper _output;

        public B3FormatTest(ITestOutputHelper output)
        {
            _output = output;

        }

        [Fact]
        public void Serialize_SampledContext()
        {
            var carrier = new Dictionary<string, string>();
            b3Format.Inject(new SpanContext(TraceId, SpanId, TraceOptions), carrier, setter);
            ContainsExactly(carrier, new Dictionary<string, string>() { { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 }, { B3Format.XB3Sampled, "1" } });
        }

        [Fact]
        public void Serialize_NotSampledContext()
        {
            var carrier = new Dictionary<string, string>();
            var context = new SpanContext(TraceId, SpanId, ActivityTraceFlags.None);
            _output.WriteLine(context.ToString());
            b3Format.Inject(context, carrier, setter);
            ContainsExactly(carrier, new Dictionary<string, string>() { { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 } });
        }

        [Fact]
        public void ParseMissingSampledAndMissingFlag()
        {
            var headersNotSampled = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, SpanIdBase16},
            };
            var spanContext = new SpanContext(TraceId, SpanId, ActivityTraceFlags.None);
            Assert.Equal(spanContext, b3Format.Extract(headersNotSampled, getter));
        }

        [Fact]
        public void ParseSampled()
        {
            var headersSampled = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, SpanIdBase16}, {B3Format.XB3Sampled, "1"},
            };
            Assert.Equal(new SpanContext(TraceId, SpanId, TraceOptions), b3Format.Extract(headersSampled, getter));
        }

        [Fact]
        public void ParseZeroSampled()
        {
            var headersNotSampled = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, SpanIdBase16}, {B3Format.XB3Sampled, "0"},
            };
            Assert.Equal(new SpanContext(TraceId, SpanId, ActivityTraceFlags.None), b3Format.Extract(headersNotSampled, getter));
        }

        [Fact]
        public void ParseFlag()
        {
            var headersFlagSampled = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, SpanIdBase16}, {B3Format.XB3Flags, "1"},
            };
            Assert.Equal(new SpanContext(TraceId, SpanId, TraceOptions), b3Format.Extract(headersFlagSampled, getter));
        }

        [Fact]
        public void ParseZeroFlag()
        {
            var headersFlagNotSampled = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, SpanIdBase16}, {B3Format.XB3Flags, "0"},
            };
            Assert.Equal(new SpanContext(TraceId, SpanId, ActivityTraceFlags.None), b3Format.Extract(headersFlagNotSampled, getter));
        }

        [Fact]
        public void ParseEightBytesTraceId()
        {
            var headersEightBytes = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16EightBytes},
                {B3Format.XB3SpanId, SpanIdBase16},
                {B3Format.XB3Sampled, "1"},
            };
            Assert.Equal(new SpanContext(TraceIdEightBytes, SpanId, TraceOptions), b3Format.Extract(headersEightBytes, getter));
        }

        [Fact]
        public void ParseEightBytesTraceId_NotSampledSpanContext()
        {
            var headersEightBytes = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16EightBytes}, {B3Format.XB3SpanId, SpanIdBase16},
            };
            Assert.Equal(new SpanContext(TraceIdEightBytes, SpanId, ActivityTraceFlags.None), b3Format.Extract(headersEightBytes, getter));
        }

        [Fact]
        public void ParseInvalidTraceId()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, "abcdefghijklmnop"}, {B3Format.XB3SpanId, SpanIdBase16},
            };
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseInvalidTraceId_Size()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, "0123456789abcdef00"}, {B3Format.XB3SpanId, SpanIdBase16},
            };
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseMissingTraceId()
        {
            var invalidHeaders = new Dictionary<string, string> {{B3Format.XB3SpanId, SpanIdBase16},};
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseInvalidSpanId()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, "abcdefghijklmnop"},
            };
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseInvalidSpanId_Size()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                {B3Format.XB3TraceId, TraceIdBase16}, {B3Format.XB3SpanId, "0123456789abcdef00"},
            };
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseMissingSpanId()
        {
            var invalidHeaders = new Dictionary<string, string> {{B3Format.XB3TraceId, TraceIdBase16}};
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void Fields_list()
        {
            ContainsExactly(b3Format.Fields,
                new List<string>() { B3Format.XB3TraceId, B3Format.XB3SpanId, B3Format.XB3ParentSpanId, B3Format.XB3Sampled, B3Format.XB3Flags });
        }

        private void ContainsExactly(ISet<string> list, List<string> items)
        {
            Assert.Equal(items.Count, list.Count);
            foreach (var item in items)
            {
                Assert.Contains(item, list);
            }
        }

        private void ContainsExactly(IDictionary<string, string> dict, IDictionary<string, string> items)
        {
            foreach (var d in dict)
            {
                _output.WriteLine(d.Key + "=" + d.Value);
            }

            Assert.Equal(items.Count, dict.Count);
            foreach (var item in items)
            {
                Assert.Contains(item, dict);
            }
        }
    }
}

