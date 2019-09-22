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
            var profiledCommand = new Mock<IProfiledCommand>();
            var tracer = Tracing.Tracer;

            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");

            var result = (Span)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(tracer, BlankSpan.Instance, profiledCommand.Object);
            Assert.Equal("SET", result.Name);
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesTimestampAsStartTime()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var now = DateTimeOffset.Now;
            profiledCommand.Setup(m => m.CommandCreated).Returns(now.DateTime);
            var result = ((Span)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(Tracing.Tracer, BlankSpan.Instance, profiledCommand.Object)).ToSpanData();
            Assert.Equal(now, result.StartTimestamp);
        }

        [Fact]
        public void ProfiledCommandToSpanDataSetsDbTypeAttributeAsRedis()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            var result = ((Span)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(Tracing.Tracer, BlankSpan.Instance, profiledCommand.Object)).ToSpanData();
            Assert.Contains(result.Attributes.AttributeMap, kvp => kvp.Key == "db.type");
            Assert.Equal("redis", result.Attributes.GetValue("db.type"));
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesCommandAsDbStatementAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = ((Span)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(Tracing.Tracer, BlankSpan.Instance, profiledCommand.Object)).ToSpanData();
            Assert.Contains(result.Attributes.AttributeMap, kvp => kvp.Key == "db.statement");
            Assert.Equal("SET", result.Attributes.GetValue("db.statement"));
        }

        [Fact]
        public void ProfiledCommandToSpanDataUsesFlagsForFlagsAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            var expectedFlags = StackExchange.Redis.CommandFlags.FireAndForget |
                                StackExchange.Redis.CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);
            var result = ((Span)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(Tracing.Tracer, BlankSpan.Instance, profiledCommand.Object)).ToSpanData();
            Assert.Contains(result.Attributes.AttributeMap, kvp => kvp.Key == "redis.flags");
            Assert.Equal("None, FireAndForget, NoRedirect", result.Attributes.GetValue("redis.flags"));
        }
    }
}
