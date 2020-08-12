// <copyright file="B3FormatTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Context.Propagation.Test
{
    public class B3FormatTest
    {
        private const string TraceIdBase16 = "ff000000000000000000000000000041";
        private const string TraceIdBase16EightBytes = "0000000000000041";
        private const string SpanIdBase16 = "ff00000000000041";
        private const string InvalidId = "abcdefghijklmnop";
        private const string InvalidSizeId = "0123456789abcdef00";
        private const ActivityTraceFlags TraceOptions = ActivityTraceFlags.Recorded;

        private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateFromString(TraceIdBase16.AsSpan());
        private static readonly ActivityTraceId TraceIdEightBytes = ActivityTraceId.CreateFromString(("0000000000000000" + TraceIdBase16EightBytes).AsSpan());
        private static readonly ActivitySpanId SpanId = ActivitySpanId.CreateFromString(SpanIdBase16.AsSpan());

        private static readonly Action<IDictionary<string, string>, string, string> Setter = (d, k, v) => d[k] = v;
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
            (d, k) =>
            {
                d.TryGetValue(k, out var v);
                return new string[] { v };
            };

        private readonly B3Format b3Format = new B3Format();
        private readonly B3Format b3FormatSingleHeader = new B3Format(true);

        private readonly ITestOutputHelper output;

        public B3FormatTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Serialize_SampledContext()
        {
            var carrier = new Dictionary<string, string>();
            this.b3Format.Inject(new ActivityContext(TraceId, SpanId, TraceOptions), carrier, Setter);
            this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 }, { B3Format.XB3Sampled, "1" } });
        }

        [Fact]
        public void Serialize_NotSampledContext()
        {
            var carrier = new Dictionary<string, string>();
            var context = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None);
            this.output.WriteLine(context.ToString());
            this.b3Format.Inject(context, carrier, Setter);
            this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 } });
        }

        [Fact]
        public void ParseMissingSampledAndMissingFlag()
        {
            var headersNotSampled = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 },
            };
            var spanContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None);
            Assert.Equal(spanContext, this.b3Format.Extract(default, headersNotSampled, Getter));
        }

        [Fact]
        public void ParseSampled()
        {
            var headersSampled = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 }, { B3Format.XB3Sampled, "1" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, TraceOptions), this.b3Format.Extract(default, headersSampled, Getter));
        }

        [Fact]
        public void ParseZeroSampled()
        {
            var headersNotSampled = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 }, { B3Format.XB3Sampled, "0" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None), this.b3Format.Extract(default, headersNotSampled, Getter));
        }

        [Fact]
        public void ParseFlag()
        {
            var headersFlagSampled = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 }, { B3Format.XB3Flags, "1" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, TraceOptions), this.b3Format.Extract(default, headersFlagSampled, Getter));
        }

        [Fact]
        public void ParseZeroFlag()
        {
            var headersFlagNotSampled = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, SpanIdBase16 }, { B3Format.XB3Flags, "0" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None), this.b3Format.Extract(default, headersFlagNotSampled, Getter));
        }

        [Fact]
        public void ParseEightBytesTraceId()
        {
            var headersEightBytes = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16EightBytes },
                { B3Format.XB3SpanId, SpanIdBase16 },
                { B3Format.XB3Sampled, "1" },
            };
            Assert.Equal(new ActivityContext(TraceIdEightBytes, SpanId, TraceOptions), this.b3Format.Extract(default, headersEightBytes, Getter));
        }

        [Fact]
        public void ParseEightBytesTraceId_NotSampledSpanContext()
        {
            var headersEightBytes = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16EightBytes }, { B3Format.XB3SpanId, SpanIdBase16 },
            };
            Assert.Equal(new ActivityContext(TraceIdEightBytes, SpanId, ActivityTraceFlags.None), this.b3Format.Extract(default, headersEightBytes, Getter));
        }

        [Fact]
        public void ParseInvalidTraceId()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, InvalidId }, { B3Format.XB3SpanId, SpanIdBase16 },
            };
            Assert.Equal(default, this.b3Format.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseInvalidTraceId_Size()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, InvalidSizeId }, { B3Format.XB3SpanId, SpanIdBase16 },
            };

            Assert.Equal(default, this.b3Format.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseMissingTraceId()
        {
            var invalidHeaders = new Dictionary<string, string> { { B3Format.XB3SpanId, SpanIdBase16 }, };
            Assert.Equal(default, this.b3Format.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseInvalidSpanId()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, InvalidId },
            };
            Assert.Equal(default, this.b3Format.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseInvalidSpanId_Size()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3TraceId, TraceIdBase16 }, { B3Format.XB3SpanId, InvalidSizeId },
            };
            Assert.Equal(default, this.b3Format.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseMissingSpanId()
        {
            var invalidHeaders = new Dictionary<string, string> { { B3Format.XB3TraceId, TraceIdBase16 } };
            Assert.Equal(default, this.b3Format.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void Serialize_SampledContext_SingleHeader()
        {
            var carrier = new Dictionary<string, string>();
            this.b3FormatSingleHeader.Inject(new ActivityContext(TraceId, SpanId, TraceOptions), carrier, Setter);
            this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-1" } });
        }

        [Fact]
        public void Serialize_NotSampledContext_SingleHeader()
        {
            var carrier = new Dictionary<string, string>();
            var context = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None);
            this.output.WriteLine(context.ToString());
            this.b3FormatSingleHeader.Inject(context, carrier, Setter);
            this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}" } });
        }

        [Fact]
        public void ParseMissingSampledAndMissingFlag_SingleHeader()
        {
            var headersNotSampled = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}" },
            };
            var spanContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None);
            Assert.Equal(spanContext, this.b3FormatSingleHeader.Extract(default, headersNotSampled, Getter));
        }

        [Fact]
        public void ParseSampled_SingleHeader()
        {
            var headersSampled = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-1" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, TraceOptions), this.b3FormatSingleHeader.Extract(default, headersSampled, Getter));
        }

        [Fact]
        public void ParseZeroSampled_SingleHeader()
        {
            var headersNotSampled = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-0" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None), this.b3FormatSingleHeader.Extract(default, headersNotSampled, Getter));
        }

        [Fact]
        public void ParseFlag_SingleHeader()
        {
            var headersFlagSampled = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-1" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, TraceOptions), this.b3FormatSingleHeader.Extract(default, headersFlagSampled, Getter));
        }

        [Fact]
        public void ParseZeroFlag_SingleHeader()
        {
            var headersFlagNotSampled = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-0" },
            };
            Assert.Equal(new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None), this.b3FormatSingleHeader.Extract(default, headersFlagNotSampled, Getter));
        }

        [Fact]
        public void ParseEightBytesTraceId_SingleHeader()
        {
            var headersEightBytes = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16EightBytes}-{SpanIdBase16}-1" },
            };
            Assert.Equal(new ActivityContext(TraceIdEightBytes, SpanId, TraceOptions), this.b3FormatSingleHeader.Extract(default, headersEightBytes, Getter));
        }

        [Fact]
        public void ParseEightBytesTraceId_NotSampledSpanContext_SingleHeader()
        {
            var headersEightBytes = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16EightBytes}-{SpanIdBase16}" },
            };
            Assert.Equal(new ActivityContext(TraceIdEightBytes, SpanId, ActivityTraceFlags.None), this.b3FormatSingleHeader.Extract(default, headersEightBytes, Getter));
        }

        [Fact]
        public void ParseInvalidTraceId_SingleHeader()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{InvalidId}-{SpanIdBase16}" },
            };
            Assert.Equal(default, this.b3FormatSingleHeader.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseInvalidTraceId_Size_SingleHeader()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{InvalidSizeId}-{SpanIdBase16}" },
            };

            Assert.Equal(default, this.b3FormatSingleHeader.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseMissingTraceId_SingleHeader()
        {
            var invalidHeaders = new Dictionary<string, string> { { B3Format.XB3Combined, $"-{SpanIdBase16}" } };
            Assert.Equal(default, this.b3FormatSingleHeader.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseInvalidSpanId_SingleHeader()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{InvalidId}" },
            };
            Assert.Equal(default, this.b3FormatSingleHeader.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseInvalidSpanId_Size_SingleHeader()
        {
            var invalidHeaders = new Dictionary<string, string>
            {
                { B3Format.XB3Combined, $"{TraceIdBase16}-{InvalidSizeId}" },
            };
            Assert.Equal(default, this.b3FormatSingleHeader.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void ParseMissingSpanId_SingleHeader()
        {
            var invalidHeaders = new Dictionary<string, string> { { B3Format.XB3Combined, $"{TraceIdBase16}-" } };
            Assert.Equal(default, this.b3FormatSingleHeader.Extract(default, invalidHeaders, Getter));
        }

        [Fact]
        public void Fields_list()
        {
            this.ContainsExactly(
                this.b3Format.Fields,
                new List<string> { B3Format.XB3TraceId, B3Format.XB3SpanId, B3Format.XB3ParentSpanId, B3Format.XB3Sampled, B3Format.XB3Flags });
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
                this.output.WriteLine(d.Key + "=" + d.Value);
            }

            Assert.Equal(items.Count, dict.Count);
            foreach (var item in items)
            {
                Assert.Contains(item, dict);
            }
        }
    }
}
