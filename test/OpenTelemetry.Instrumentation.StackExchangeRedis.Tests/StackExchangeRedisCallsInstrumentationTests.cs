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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests
{
    [Collection("Redis")]
    public class StackExchangeRedisCallsInstrumentationTests
    {
        /*
            To run the integration tests, set the OT_REDISENDPOINT machine-level environment variable to a valid Redis endpoint.

            To use Docker...
             1) Run: docker run -d --name redis -p 6379:6379 redis
             2) Set OT_REDISENDPOINT as: localhost:6379
         */

        private const string RedisEndPointEnvVarName = "OT_REDISENDPOINT";
        private static readonly string RedisEndPoint = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(RedisEndPointEnvVarName);

        [Trait("CategoryName", "RedisIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(RedisEndPointEnvVarName)]
        [InlineData("value1")]
        public void SuccessfulCommandTest(string value)
        {
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = true,
            };
            connectionOptions.EndPoints.Add(RedisEndPoint);

            using var connection = ConnectionMultiplexer.Connect(connectionOptions);

            var activityProcessor = new Mock<ActivityProcessor>();
            using (OpenTelemetrySdk.EnableOpenTelemetry(b =>
            {
                b.AddProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddRedisInstrumentation(connection);
            }))
            {
                IDatabase db = connection.GetDatabase();

                bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

                Assert.True(set);

                var redisValue = db.StringGet("key1");

                Assert.True(redisValue.HasValue);
                Assert.Equal(value, redisValue.ToString());
            }

            // Disposing SDK should flush the Redis profiling session immediately.

            Assert.Equal(4, activityProcessor.Invocations.Count);

            VerifyActivityData((Activity)activityProcessor.Invocations[1].Arguments[0], true, connection.GetEndPoints()[0]);
            VerifyActivityData((Activity)activityProcessor.Invocations[3].Arguments[0], false, connection.GetEndPoints()[0]);
        }

        [Fact]
        public async void ProfilerSessionUsesTheSameDefault()
        {
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
            };
            connectionOptions.EndPoints.Add("localhost:6379");

            var connection = ConnectionMultiplexer.Connect(connectionOptions);

            using var instrumentation = new StackExchangeRedisCallsInstrumentation(connection, new StackExchangeRedisCallsInstrumentationOptions());
            var profilerFactory = instrumentation.GetProfilerSessionsFactory();
            var first = profilerFactory();
            var second = profilerFactory();
            ProfilingSession third = null;
            await Task.Delay(1).ContinueWith((t) => { third = profilerFactory(); });
            Assert.Equal(first, second);
            Assert.Equal(second, third);
        }

        private static void VerifyActivityData(Activity activity, bool isSet, EndPoint endPoint)
        {
            if (isSet)
            {
                Assert.Equal("SETEX", activity.DisplayName);
                Assert.Equal("SETEX", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
            }
            else
            {
                Assert.Equal("GET", activity.DisplayName);
                Assert.Equal("GET", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
            }

            Assert.Equal(SpanHelper.GetCachedCanonicalCodeString(StatusCanonicalCode.Ok), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
            Assert.Equal("redis", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseSystemKey).Value);
            Assert.Equal("0", activity.Tags.FirstOrDefault(t => t.Key == StackExchangeRedisCallsInstrumentation.RedisDatabaseIndexKeyName).Value);

            if (endPoint is IPEndPoint ipEndPoint)
            {
                Assert.Equal(ipEndPoint.Address.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.NetPeerIp).Value);
                Assert.Equal(ipEndPoint.Port.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.NetPeerPort).Value);
            }
            else if (endPoint is DnsEndPoint dnsEndPoint)
            {
                Assert.Equal(dnsEndPoint.Host, activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.NetPeerName).Value);
                Assert.Equal(dnsEndPoint.Port.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.NetPeerPort).Value);
            }
            else
            {
                Assert.Equal(endPoint.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.PeerServiceKey).Value);
            }
        }
    }
}
