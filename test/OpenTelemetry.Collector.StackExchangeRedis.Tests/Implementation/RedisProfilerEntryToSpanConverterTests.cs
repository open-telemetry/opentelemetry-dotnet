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

using System.Linq;
using OpenTelemetry.Collector.StackExchangeRedis.Tests;

namespace OpenTelemetry.Collector.StackExchangeRedis.Implementation
{
    using OpenTelemetry.Trace;
    using Moq;
    using StackExchange.Redis.Profiling;
    using Xunit;
    using System;
    using System.Diagnostics;
    using System.Collections.Generic;

    public class RedisProfilerEntryToSpanConverterTests
    {
        [Fact]
        public void DrainSessionUsesCommandAsName()
        {
            var parentSpan = BlankSpan.Instance;
            var profiledCommand = new Mock<IProfiledCommand>();
            var sampler = new Mock<ISampler>();
            sampler.Setup(x => x.ShouldSample(It.IsAny<SpanContext>(), It.IsAny<ActivityTraceId>(), It.IsAny<ActivitySpanId>(), It.IsAny<string>(), It.IsAny<IEnumerable<ILink>>())).Returns(true);
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
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "SET", default, profiledCommand.Object);
            Assert.Equal(now, result.StartTimestamp);
        }

        [Fact]
        public void ProfiledCommandToSpanDataSetsDbTypeAttributeAsRedis()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "SET", default, profiledCommand.Object);
            Assert.Contains(result.Attributes.AttributeMap, kvp => kvp.Key == "db.type");
            Assert.Equal("redis", result.Attributes.GetValue("db.type"));
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesCommandAsDbStatementAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "another name", default, profiledCommand.Object);
            Assert.Contains(result.Attributes.AttributeMap, kvp => kvp.Key == "db.statement");
            Assert.Equal("SET", result.Attributes.GetValue("db.statement"));
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesFlagsForFlagsAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var expectedFlags = StackExchange.Redis.CommandFlags.FireAndForget |
                                StackExchange.Redis.CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);
            var result =
                RedisProfilerEntryToSpanConverter.ProfiledCommandToSpanData(SpanContext.Blank, "SET", default,
                    profiledCommand.Object);
            Assert.Contains(result.Attributes.AttributeMap, kvp => kvp.Key == "redis.flags");
            Assert.Equal("None, FireAndForget, NoRedirect", result.Attributes.GetValue("redis.flags"));
        }
    }
}
