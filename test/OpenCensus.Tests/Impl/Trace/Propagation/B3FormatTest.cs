// <copyright file="B3FormatTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Propagation.Test
{
    using System;
    using System.Collections.Generic;
    using Xunit;
    using Xunit.Abstractions;

    public class B3FormatTest
    {
        private static readonly string TRACE_ID_BASE16 = "ff000000000000000000000000000041";
        private static readonly ITraceId TRACE_ID = TraceId.FromLowerBase16(TRACE_ID_BASE16);
        private static readonly string TRACE_ID_BASE16_EIGHT_BYTES = "0000000000000041";
        private static readonly ITraceId TRACE_ID_EIGHT_BYTES = TraceId.FromLowerBase16("0000000000000000" + TRACE_ID_BASE16_EIGHT_BYTES);
        private static readonly string SPAN_ID_BASE16 = "ff00000000000041";
        private static readonly ISpanId SPAN_ID = SpanId.FromLowerBase16(SPAN_ID_BASE16);
        private static readonly byte[] TRACE_OPTIONS_BYTES = new byte[] { 1 };
        private static readonly TraceOptions TRACE_OPTIONS = TraceOptions.FromBytes(TRACE_OPTIONS_BYTES);
        private readonly B3Format b3Format = new B3Format();


        private static readonly Action<IDictionary<string, string>, string, string> setter = (d, k, v) => d[k] = v;
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> getter = (d, k) => { d.TryGetValue(k, out string v); return new string[] { v }; };
        ITestOutputHelper _output;

        public B3FormatTest(ITestOutputHelper output)
        {
            _output = output;

        }

        [Fact]
        public void Serialize_SampledContext()
        {
            IDictionary<String, String> carrier = new Dictionary<String, String>();
            b3Format.Inject(SpanContext.Create(TRACE_ID, SPAN_ID, TRACE_OPTIONS, Tracestate.Empty), carrier, setter);
            ContainsExactly(carrier, new Dictionary<string, string>() { { B3Format.XB3TraceId, TRACE_ID_BASE16 }, { B3Format.XB3SpanId, SPAN_ID_BASE16 }, { B3Format.XB3Sampled, "1" } });
        }

        [Fact]
        public void Serialize_NotSampledContext()
        {
            IDictionary<String, String> carrier = new Dictionary<String, String>();
            var context = SpanContext.Create(TRACE_ID, SPAN_ID, TraceOptions.Default, Tracestate.Empty);
            _output.WriteLine(context.ToString());
            b3Format.Inject(context, carrier, setter);
            ContainsExactly(carrier, new Dictionary<string, string>() { { B3Format.XB3TraceId, TRACE_ID_BASE16 }, { B3Format.XB3SpanId, SPAN_ID_BASE16 } });
        }

        [Fact]
        public void ParseMissingSampledAndMissingFlag()
        {
            IDictionary<String, String> headersNotSampled = new Dictionary<String, String>();
            headersNotSampled.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            headersNotSampled.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            ISpanContext spanContext = SpanContext.Create(TRACE_ID, SPAN_ID, TraceOptions.Default, Tracestate.Empty);
            Assert.Equal(spanContext, b3Format.Extract(headersNotSampled, getter));
        }

        [Fact]
        public void ParseSampled()
        {
            IDictionary<String, String> headersSampled = new Dictionary<String, String>();
            headersSampled.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            headersSampled.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            headersSampled.Add(B3Format.XB3Sampled, "1");
            Assert.Equal(SpanContext.Create(TRACE_ID, SPAN_ID, TRACE_OPTIONS, Tracestate.Empty), b3Format.Extract(headersSampled, getter));
        }

        [Fact]
        public void ParseZeroSampled()
        {
            IDictionary<String, String> headersNotSampled = new Dictionary<String, String>();
            headersNotSampled.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            headersNotSampled.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            headersNotSampled.Add(B3Format.XB3Sampled, "0");
            Assert.Equal(SpanContext.Create(TRACE_ID, SPAN_ID, TraceOptions.Default, Tracestate.Empty), b3Format.Extract(headersNotSampled, getter));
        }

        [Fact]
        public void ParseFlag()
        {
            IDictionary<String, String> headersFlagSampled = new Dictionary<String, String>();
            headersFlagSampled.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            headersFlagSampled.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            headersFlagSampled.Add(B3Format.XB3Flags, "1");
            Assert.Equal(SpanContext.Create(TRACE_ID, SPAN_ID, TRACE_OPTIONS, Tracestate.Empty), b3Format.Extract(headersFlagSampled, getter));
        }

        [Fact]
        public void ParseZeroFlag()
        {
            IDictionary<String, String> headersFlagNotSampled = new Dictionary<String, String>();
            headersFlagNotSampled.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            headersFlagNotSampled.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            headersFlagNotSampled.Add(B3Format.XB3Flags, "0");
            Assert.Equal(SpanContext.Create(TRACE_ID, SPAN_ID, TraceOptions.Default, Tracestate.Empty), b3Format.Extract(headersFlagNotSampled, getter));
        }

        [Fact]
        public void ParseEightBytesTraceId()
        {
            IDictionary<String, String> headersEightBytes = new Dictionary<String, String>();
            headersEightBytes.Add(B3Format.XB3TraceId, TRACE_ID_BASE16_EIGHT_BYTES);
            headersEightBytes.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            headersEightBytes.Add(B3Format.XB3Sampled, "1");
            Assert.Equal(SpanContext.Create(TRACE_ID_EIGHT_BYTES, SPAN_ID, TRACE_OPTIONS, Tracestate.Empty), b3Format.Extract(headersEightBytes, getter));
        }

        [Fact]
        public void ParseEightBytesTraceId_NotSampledSpanContext()
        {
            IDictionary<String, String> headersEightBytes = new Dictionary<String, String>();
            headersEightBytes.Add(B3Format.XB3TraceId, TRACE_ID_BASE16_EIGHT_BYTES);
            headersEightBytes.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            Assert.Equal(SpanContext.Create(TRACE_ID_EIGHT_BYTES, SPAN_ID, TraceOptions.Default, Tracestate.Empty), b3Format.Extract(headersEightBytes, getter));
        }

        [Fact]
        public void ParseInvalidTraceId()
        {
            IDictionary<String, String> invalidHeaders = new Dictionary<String, String>();
            invalidHeaders.Add(B3Format.XB3TraceId, "abcdefghijklmnop");
            invalidHeaders.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseInvalidTraceId_Size()
        {
            IDictionary<String, String> invalidHeaders = new Dictionary<String, String>();
            invalidHeaders.Add(B3Format.XB3TraceId, "0123456789abcdef00");
            invalidHeaders.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseMissingTraceId()
        {
            IDictionary<String, String> invalidHeaders = new Dictionary<String, String>();
            invalidHeaders.Add(B3Format.XB3SpanId, SPAN_ID_BASE16);
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseInvalidSpanId()
        {
            IDictionary<String, String> invalidHeaders = new Dictionary<String, String>();
            invalidHeaders.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            invalidHeaders.Add(B3Format.XB3SpanId, "abcdefghijklmnop");
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseInvalidSpanId_Size()
        {
            IDictionary<String, String> invalidHeaders = new Dictionary<String, String>();
            invalidHeaders.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
            invalidHeaders.Add(B3Format.XB3SpanId, "0123456789abcdef00");
            Assert.Throws<SpanContextParseException>(() => b3Format.Extract(invalidHeaders, getter));
        }

        [Fact]
        public void ParseMissingSpanId()
        {
            IDictionary<String, String> invalidHeaders = new Dictionary<String, String>();
            invalidHeaders.Add(B3Format.XB3TraceId, TRACE_ID_BASE16);
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

