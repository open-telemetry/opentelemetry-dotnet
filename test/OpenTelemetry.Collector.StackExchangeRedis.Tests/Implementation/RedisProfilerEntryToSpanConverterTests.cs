// <copyright file="StackExchangeRedisCallsCollectorTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.StackExchangeRedis.Implementation
{
    using OpenTelemetry.Trace;
    using Moq;
    using StackExchange.Redis.Profiling;
    using Xunit;
    using OpenTelemetry.Trace.Internal;
    using System;
    using OpenTelemetry.Common;
    using System.Collections.Generic;

    public class RedisProfilerEntryToSpanConverterTests
    {
        [Fact]
        public void DrainSessionUsesCommandAsName()
        {
            var parentSpan = BlankSpan.Instance;
            var profiledCommand = new Mock<IProfiledCommand>();
            var sampler = new Mock<ISampler>();
            sampler.Setup(x => x.ShouldSample(It.IsAny<SpanContext>(), It.IsAny<TraceId>(), It.IsAny<SpanId>(), It.IsAny<string>(), It.IsAny<IEnumerable<ILink>>())).Returns(true);
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = new List<SpanData>();
            RedisProfilerEntryToSpanConverter.DrainSession(parentSpan, new IProfiledCommand[] { profiledCommand.Object }, sampler.Object, result);
            Assert.Single(result);
            Assert.Equal("SET", result[0].Name);
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesTimestampAsStartTime()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var now = DateTimeOffset.Now;
            profiledCommand.Setup(m => m.CommandCreated).Returns(now.DateTime);
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "SET", SpanId.Invalid, profiledCommand.Object);
            Assert.Equal(Timestamp.FromMillis(now.ToUnixTimeMilliseconds()), result.StartTimestamp);
        }

        [Fact]
        public void ProfiledCommandToSpanDataSetsDbTypeAttributeAsRedis()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "SET", SpanId.Invalid, profiledCommand.Object);
            Assert.Contains("db.type", result.Attributes.AttributeMap.Keys);
            Assert.Equal(AttributeValue.StringAttributeValue("redis"), result.Attributes.AttributeMap["db.type"]);
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesCommandAsDbStatementAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "another name", SpanId.Invalid, profiledCommand.Object);
            Assert.Contains("db.statement", result.Attributes.AttributeMap.Keys);
            Assert.Equal(AttributeValue.StringAttributeValue("SET"), result.Attributes.AttributeMap["db.statement"]);
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesFlagsForFlagsAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var expectedFlags = StackExchange.Redis.CommandFlags.FireAndForget | StackExchange.Redis.CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "SET", SpanId.Invalid, profiledCommand.Object);
            Assert.Contains("redis.flags", result.Attributes.AttributeMap.Keys);
            Assert.Equal(AttributeValue.StringAttributeValue("None, FireAndForget, NoRedirect"), result.Attributes.AttributeMap["redis.flags"]);
        }
    }
}
