// <copyright file="ActivityExtensionsTest.cs" company="Microsoft">
// Copyright (c) .NET Foundation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// </copyright>

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using Xunit;

    public class ActivityExtensionsTest
    {
        private const string TestActivityName = "Activity.Test";

        [Fact]
        public void Restore_Nothing_If_Header_Does_Not_Contain_RequestId()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();

            Assert.False(activity.Extract(requestHeaders));

            Assert.True(string.IsNullOrEmpty(activity.ParentId));
            Assert.Null(activity.TraceStateString);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Restore_First_RequestId_When_Multiple_RequestId_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b22222.1" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Extract_RequestId_Is_Ignored_When_Traceparent_Is_Present()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00" }
            };
            Assert.True(activity.Extract(requestHeaders));

            activity.Start();
            Assert.Equal(ActivityIdFormat.W3C, activity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-00", activity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", activity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", activity.ParentSpanId.ToHexString());
            Assert.False(activity.Recorded);

            Assert.Null(activity.TraceStateString);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Extract_First_Traceparent_When_Multiple_Traceparents_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00" },
                { ActivityExtensions.TraceparentHeaderName, "00-fedcba09876543210fedcba09876543210-fedcba09876543210-01" }
            };
            Assert.True(activity.Extract(requestHeaders));

            activity.Start();
            Assert.Equal(ActivityIdFormat.W3C, activity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-00", activity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", activity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", activity.ParentSpanId.ToHexString());
            Assert.False(activity.Recorded);

            Assert.Null(activity.TraceStateString);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Extract_RootActivity_From_W3C_Headers_And_CC()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01" },
                { ActivityExtensions.TracestateHeaderName, "ts1=v1,ts2=v2" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" },
            };

            Assert.True(activity.Extract(requestHeaders));
            activity.Start();
            Assert.Equal(ActivityIdFormat.W3C, activity.IdFormat);
            Assert.Equal("0123456789abcdef0123456789abcdef", activity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", activity.ParentSpanId.ToHexString());
            Assert.True(activity.Recorded);

            Assert.Equal("ts1=v1,ts2=v2", activity.TraceStateString);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Extract_Empty_Traceparent()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.TraceparentHeaderName, string.Empty },
            };

            Assert.False(activity.Extract(requestHeaders));

            Assert.Equal(default, activity.ParentSpanId);
            Assert.Null(activity.ParentId);
        }

        [Fact]
        public void Can_Extract_Multi_Line_Tracestate()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01" },
                { ActivityExtensions.TracestateHeaderName, "ts1=v1" },
                { ActivityExtensions.TracestateHeaderName, "ts2=v2" },
            };

            Assert.True(activity.Extract(requestHeaders));
            activity.Start();
            Assert.Equal(ActivityIdFormat.W3C, activity.IdFormat);
            Assert.Equal("0123456789abcdef0123456789abcdef", activity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", activity.ParentSpanId.ToHexString());
            Assert.True(activity.Recorded);

            Assert.Equal("ts1=v1,ts2=v2", activity.TraceStateString);
        }

        [Fact]
        public void Restore_Empty_RequestId_Should_Not_Throw_Exception()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, string.Empty }
            };
            Assert.False(activity.Extract(requestHeaders));

            Assert.Null(activity.ParentId);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Restore_Empty_Traceparent_Should_Not_Throw_Exception()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.TraceparentHeaderName, string.Empty }
            };
            Assert.False(activity.Extract(requestHeaders));

            Assert.Null(activity.ParentId);
            Assert.Null(activity.TraceStateString);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_Multiple_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" },
                { ActivityExtensions.CorrelationContextHeaderName, "key4=abc,key5=def" },
                { ActivityExtensions.CorrelationContextHeaderName, "key6=xyz" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789"),
                new KeyValuePair<string, string>("key4", "abc"),
                new KeyValuePair<string, string>("key5", "def"),
                new KeyValuePair<string, string>("key6", "xyz")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_Some_MalFormat_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" },
                { ActivityExtensions.CorrelationContextHeaderName, "key4=abc;key5=def" },
                { ActivityExtensions.CorrelationContextHeaderName, "key6????xyz" },
                { ActivityExtensions.CorrelationContextHeaderName, "key7=123=456" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Theory]
        [InlineData(
            "key0=value0,key1=value1,key2=value2,key3=value3,key4=value4,key5=value5,key6=value6,key7=value7,key8=value8,key9=value9," +
                    "key10=value10,key11=value11,key12=value12,key13=value13,key14=value14,key15=value15,key16=value16,key17=value17,key18=value18,key19=value19," +
                    "key20=value20,key21=value21,key22=value22,key23=value23,key24=value24,key25=value25,key26=value26,key27=value27,key28=value28,key29=value29," +
                    "key30=value30,key31=value31,key32=value32,key33=value33,key34=value34,key35=value35,key36=value36,key37=value37,key38=value38,key39=value39," +
                    "key40=value40,key41=value41,key42=value42,key43=value43,key44=value44,key45=value45,key46=value46,key47=value47,key48=value48,key49=value49," +
                    "key50=value50,key51=value51,key52=value52,key53=value53,key54=value54,key55=value55,key56=value56,key57=value57,key58=value58,key59=value59," +
                    "key60=value60,key61=value61,key62=value62,key63=value63,key64=value64,key65=value65,key66=value66,key67=value67,key68=value68,key69=value69," +
                    "key70=value70,key71=value71,key72=value72,key73=value73,k100=vx", 1023)] // 1023 chars
        [InlineData(
            "key0=value0,key1=value1,key2=value2,key3=value3,key4=value4,key5=value5,key6=value6,key7=value7,key8=value8,key9=value9," +
                    "key10=value10,key11=value11,key12=value12,key13=value13,key14=value14,key15=value15,key16=value16,key17=value17,key18=value18,key19=value19," +
                    "key20=value20,key21=value21,key22=value22,key23=value23,key24=value24,key25=value25,key26=value26,key27=value27,key28=value28,key29=value29," +
                    "key30=value30,key31=value31,key32=value32,key33=value33,key34=value34,key35=value35,key36=value36,key37=value37,key38=value38,key39=value39," +
                    "key40=value40,key41=value41,key42=value42,key43=value43,key44=value44,key45=value45,key46=value46,key47=value47,key48=value48,key49=value49," +
                    "key50=value50,key51=value51,key52=value52,key53=value53,key54=value54,key55=value55,key56=value56,key57=value57,key58=value58,key59=value59," +
                    "key60=value60,key61=value61,key62=value62,key63=value63,key64=value64,key65=value65,key66=value66,key67=value67,key68=value68,key69=value69," +
                    "key70=value70,key71=value71,key72=value72,key73=value73,k100=vx1", 1024)] // 1024 chars
        [InlineData(
            "key0=value0,key1=value1,key2=value2,key3=value3,key4=value4,key5=value5,key6=value6,key7=value7,key8=value8,key9=value9," +
                    "key10=value10,key11=value11,key12=value12,key13=value13,key14=value14,key15=value15,key16=value16,key17=value17,key18=value18,key19=value19," +
                    "key20=value20,key21=value21,key22=value22,key23=value23,key24=value24,key25=value25,key26=value26,key27=value27,key28=value28,key29=value29," +
                    "key30=value30,key31=value31,key32=value32,key33=value33,key34=value34,key35=value35,key36=value36,key37=value37,key38=value38,key39=value39," +
                    "key40=value40,key41=value41,key42=value42,key43=value43,key44=value44,key45=value45,key46=value46,key47=value47,key48=value48,key49=value49," +
                    "key50=value50,key51=value51,key52=value52,key53=value53,key54=value54,key55=value55,key56=value56,key57=value57,key58=value58,key59=value59," +
                    "key60=value60,key61=value61,key62=value62,key63=value63,key64=value64,key65=value65,key66=value66,key67=value67,key68=value68,key69=value69," +
                    "key70=value70,key71=value71,key72=value72,key73=value73,key74=value74", 1029)] // more than 1024 chars
        public void Validates_Correlation_Context_Length(string correlationContext, int expectedLength)
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIdHeaderName, "|abc.1" },
                { ActivityExtensions.CorrelationContextHeaderName, correlationContext }
            };
            Assert.True(activity.Extract(requestHeaders));

            var baggageItems = Enumerable.Range(0, 74).Select(i => new KeyValuePair<string, string>("key" + i, "value" + i)).ToList();
            if (expectedLength < 1024)
            {
                baggageItems.Add(new KeyValuePair<string, string>("k100", "vx"));
            }

            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }
    }
}
