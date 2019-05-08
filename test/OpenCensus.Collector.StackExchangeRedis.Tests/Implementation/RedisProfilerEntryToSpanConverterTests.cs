// <copyright file="StackExchangeRedisCallsCollectorTests.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Collector.StackExchangeRedis.Implementation
{
    using OpenCensus.Trace;
    using Moq;
    using StackExchange.Redis.Profiling;
    using Xunit;
    using OpenCensus.Trace.Internal;
    using System;
    using OpenCensus.Common;
    using System.Collections.Generic;
    using OpenCensus.Trace.Export;

    public class RedisProfilerEntryToSpanConverterTests
    {
        [Fact]
        public void DrainSessionUsesCommandAsName()
        {
            var parentSpan = BlankSpan.Instance;
            var profiledCommand = new Mock<IProfiledCommand>();
            var sampler = new Mock<ISampler>();
            sampler.Setup(x => x.ShouldSample(It.IsAny<ISpanContext>(), It.IsAny<bool>(), It.IsAny<ITraceId>(), It.IsAny<ISpanId>(), It.IsAny<string>(), It.IsAny<IEnumerable<ISpan>>())).Returns(true);
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = new List<ISpanData>();
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
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Invalid, "SET", SpanId.Invalid, profiledCommand.Object);
            Assert.Equal(Timestamp.FromMillis(now.ToUnixTimeMilliseconds()), result.StartTimestamp);
        }

        [Fact]
        public void ProfiledCommandToSpanDataSetsDbTypeAttributeAsRedis()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Invalid, "SET", SpanId.Invalid, profiledCommand.Object);
            Assert.Contains("db.type", result.Attributes.AttributeMap.Keys);
            Assert.Equal(AttributeValue.StringAttributeValue("redis"), result.Attributes.AttributeMap["db.type"]);
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesCommandAsDbStatementAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Invalid, "another name", SpanId.Invalid, profiledCommand.Object);
            Assert.Contains("db.statement", result.Attributes.AttributeMap.Keys);
            Assert.Equal(AttributeValue.StringAttributeValue("SET"), result.Attributes.AttributeMap["db.statement"]);
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesFlagsForFlagsAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var expectedFlags = StackExchange.Redis.CommandFlags.FireAndForget | StackExchange.Redis.CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Invalid, "SET", SpanId.Invalid, profiledCommand.Object);
            Assert.Contains("redis.flags", result.Attributes.AttributeMap.Keys);
            Assert.Equal(AttributeValue.StringAttributeValue("None, FireAndForget, NoRedirect"), result.Attributes.AttributeMap["redis.flags"]);
        }
    }
}
