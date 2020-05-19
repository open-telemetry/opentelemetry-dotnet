// <copyright file="StackExchangeRedisCallsInstrumentationTests.cs" company="OpenTelemetry Authors">
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
using Moq;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using StackExchange.Redis.Profiling;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation
{
    public class RedisProfilerEntryToSpanConverterTests
    {
        private readonly Tracer tracer;

        public RedisProfilerEntryToSpanConverterTests()
        {
            tracer = TracerFactory.Create(b => { }).GetTracer(null);
        }

        [Fact]
        public void DrainSessionUsesCommandAsName()
        {
            var profiledCommand = new Mock<IProfiledCommand>();

            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");

            var result = (SpanSdk)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(tracer, null, profiledCommand.Object);
            Assert.Equal("SET", result.Name);
        }

        [Fact]
        public void ProfiledCommandToSpanUsesTimestampAsStartTime()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            var now = DateTimeOffset.Now;
            profiledCommand.Setup(m => m.CommandCreated).Returns(now.DateTime);
            var result = ((SpanSdk)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(this.tracer, null, profiledCommand.Object));
            Assert.Equal(now, result.StartTimestamp);
        }

        [Fact]
        public void ProfiledCommandToSpanSetsDbTypeAttributeAsRedis()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            var result = ((SpanSdk)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(this.tracer, null, profiledCommand.Object));
            Assert.Contains(result.Attributes, kvp => kvp.Key == "db.type");
            Assert.Equal("redis", result.Attributes.GetValue("db.type"));
        }

        [Fact]
        public void ProfiledCommandToSpanUsesCommandAsDbStatementAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");
            var result = ((SpanSdk)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(this.tracer, null, profiledCommand.Object));
            Assert.Contains(result.Attributes, kvp => kvp.Key == "db.statement");
            Assert.Equal("SET", result.Attributes.GetValue("db.statement"));
        }

        [Fact]
        public void ProfiledCommandToSpanUsesFlagsForFlagsAttribute()
        {
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            var expectedFlags = StackExchange.Redis.CommandFlags.FireAndForget |
                                StackExchange.Redis.CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);
            var result = ((SpanSdk)RedisProfilerEntryToSpanConverter.ProfilerCommandToSpan(this.tracer, null, profiledCommand.Object));
            Assert.Contains(result.Attributes, kvp => kvp.Key == "redis.flags");
            Assert.Equal("None, FireAndForget, NoRedirect", result.Attributes.GetValue("redis.flags"));
        }
    }
}
