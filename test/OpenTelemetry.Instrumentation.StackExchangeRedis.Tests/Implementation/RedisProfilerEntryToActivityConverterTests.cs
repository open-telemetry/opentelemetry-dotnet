// <copyright file="RedisProfilerEntryToActivityConverterTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using Moq;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation
{
    [Collection("Redis")]
    public class RedisProfilerEntryToActivityConverterTests : IDisposable
    {
        private readonly ConnectionMultiplexer connection;
        private readonly IDisposable sdk;

        public RedisProfilerEntryToActivityConverterTests()
        {
            this.connection = ConnectionMultiplexer.Connect("localhost:6379");

            this.sdk = OpenTelemetrySdk.EnableOpenTelemetry(
                (builder) => builder.AddRedisInstrumentation(this.connection));
        }

        public void Dispose()
        {
            this.sdk.Dispose();
            this.connection.Dispose();
        }

        [Fact]
        public void DrainSessionUsesCommandAsName()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Equal("SET", result.DisplayName);
        }

        [Fact]
        public void ProfiledCommandToSpanUsesTimestampAsStartTime()
        {
            var now = DateTimeOffset.Now;
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(now.DateTime);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Equal(now, result.StartTimeUtc);
        }

        [Fact]
        public void ProfiledCommandToSpanSetsDbTypeAttributeAsRedis()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Contains(result.Tags, kvp => kvp.Key == SpanAttributeConstants.DatabaseSystemKey);
            Assert.Equal("redis", result.Tags.FirstOrDefault(kvp => kvp.Key == SpanAttributeConstants.DatabaseSystemKey).Value);
        }

        [Fact]
        public void ProfiledCommandToSpanUsesCommandAsDbStatementAttribute()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Contains(result.Tags, kvp => kvp.Key == SpanAttributeConstants.DatabaseStatementKey);
            Assert.Equal("SET", result.Tags.FirstOrDefault(kvp => kvp.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
        }

        [Fact]
        public void ProfiledCommandToSpanUsesFlagsForFlagsAttribute()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            var expectedFlags = CommandFlags.FireAndForget |
                                CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Contains(result.Tags, kvp => kvp.Key == StackExchangeRedisCallsInstrumentation.RedisFlagsKeyName);
            Assert.Equal("PreferMaster, FireAndForget, NoRedirect", result.Tags.FirstOrDefault(kvp => kvp.Key == StackExchangeRedisCallsInstrumentation.RedisFlagsKeyName).Value);
        }
    }
}
