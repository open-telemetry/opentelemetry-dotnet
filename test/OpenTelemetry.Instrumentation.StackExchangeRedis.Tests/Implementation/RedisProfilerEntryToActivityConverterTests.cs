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
using System.Net;
using System.Net.Sockets;
using Moq;
using OpenTelemetry.Trace;
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
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
            };
            connectionOptions.EndPoints.Add("localhost:6379");

            this.connection = ConnectionMultiplexer.Connect(connectionOptions);

            this.sdk = Sdk.CreateTracerProviderBuilder()
                .AddRedisInstrumentation(this.connection)
                .Build();
        }

        public void Dispose()
        {
            this.sdk.Dispose();
            this.connection.Dispose();
        }

        [Fact]
        public void ProfilerCommandToActivity_UsesCommandAsName()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Equal("SET", result.DisplayName);
        }

        [Fact]
        public void ProfilerCommandToActivity_UsesTimestampAsStartTime()
        {
            var now = DateTimeOffset.Now;
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(now.DateTime);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.Equal(now, result.StartTimeUtc);
        }

        [Fact]
        public void ProfilerCommandToActivity_SetsDbTypeAttributeAsRedis()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("redis", result.GetTagValue(SemanticConventions.AttributeDbSystem));
        }

        [Fact]
        public void ProfilerCommandToActivity_UsesCommandAsDbStatementAttribute()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            profiledCommand.Setup(m => m.Command).Returns("SET");

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeDbStatement));
            Assert.Equal("SET", result.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        [Fact]
        public void ProfilerCommandToActivity_UsesFlagsForFlagsAttribute()
        {
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.CommandCreated).Returns(DateTime.UtcNow);
            var expectedFlags = CommandFlags.FireAndForget |
                                CommandFlags.NoRedirect;
            profiledCommand.Setup(m => m.Flags).Returns(expectedFlags);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.NotNull(result.GetTagValue(StackExchangeRedisCallsInstrumentation.RedisFlagsKeyName));
            Assert.Equal("PreferMaster, FireAndForget, NoRedirect", result.GetTagValue(StackExchangeRedisCallsInstrumentation.RedisFlagsKeyName));
        }

        [Fact]
        public void ProfilerCommandToActivity_UsesIpEndPointAsEndPoint()
        {
            long address = 1;
            int port = 2;

            var activity = new Activity("redis-profiler");
            IPEndPoint ipLocalEndPoint = new IPEndPoint(address, port);
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.EndPoint).Returns(ipLocalEndPoint);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerIp));
            Assert.Equal($"{address}.0.0.0", result.GetTagValue(SemanticConventions.AttributeNetPeerIp));
            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            Assert.Equal(port, result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        }

        [Fact]
        public void ProfilerCommandToActivity_UsesDnsEndPointAsEndPoint()
        {
            var dnsEndPoint = new DnsEndPoint("https://opentelemetry.io/", 443);

            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.EndPoint).Returns(dnsEndPoint);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(dnsEndPoint.Host, result.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            Assert.Equal(dnsEndPoint.Port, result.GetTagValue(SemanticConventions.AttributeNetPeerPort));
        }

#if !NET461
        [Fact]
        public void ProfilerCommandToActivity_UsesOtherEndPointAsEndPoint()
        {
            var unixEndPoint = new UnixDomainSocketEndPoint("https://opentelemetry.io/");
            var activity = new Activity("redis-profiler");
            var profiledCommand = new Mock<IProfiledCommand>();
            profiledCommand.Setup(m => m.EndPoint).Returns(unixEndPoint);

            var result = RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(activity, profiledCommand.Object);

            Assert.NotNull(result.GetTagValue(SemanticConventions.AttributePeerService));
            Assert.Equal(unixEndPoint.ToString(), result.GetTagValue(SemanticConventions.AttributePeerService));
        }
#endif
    }
}
