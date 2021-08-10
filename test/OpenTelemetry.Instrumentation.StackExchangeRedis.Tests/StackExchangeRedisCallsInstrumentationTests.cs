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
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests
{
    [Collection("Redis")]
    public class StackExchangeRedisCallsInstrumentationTests
    {
        /*
            To run the integration tests, set the OTEL_REDISENDPOINT machine-level environment variable to a valid Redis endpoint.

            To use Docker...
             1) Run: docker run -d --name redis -p 6379:6379 redis
             2) Set OTEL_REDISENDPOINT as: localhost:6379
         */

        private const string RedisEndPointEnvVarName = "OTEL_REDISENDPOINT";
        private static readonly string RedisEndPoint = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(RedisEndPointEnvVarName);

        [Trait("CategoryName", "RedisIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(RedisEndPointEnvVarName)]
        [InlineData("value1")]
        public void SuccessfulCommandTestWithKey(string value)
        {
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = true,
            };
            connectionOptions.EndPoints.Add(RedisEndPoint);

            using var connection = ConnectionMultiplexer.Connect(connectionOptions);
            var db = connection.GetDatabase();
            db.KeyDelete("key1");

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            var sampler = new TestSampler();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .SetSampler(sampler)
                .AddRedisInstrumentation(connection, c => c.SetVerboseDatabaseStatements = true)
                .Build())
            {
                var prepared = LuaScript.Prepare("redis.call('set', @key, @value)");
                db.ScriptEvaluate(prepared, new { key = (RedisKey)"mykey", value = 123 });

                var redisValue = db.StringGet("key1");

                Assert.False(redisValue.HasValue);

                bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

                Assert.True(set);

                redisValue = db.StringGet("key1");

                Assert.True(redisValue.HasValue);
                Assert.Equal(value, redisValue.ToString());
            }

            // Disposing SDK should flush the Redis profiling session immediately.

            Assert.Equal(11, activityProcessor.Invocations.Count);

            var scriptActivity = (Activity)activityProcessor.Invocations[1].Arguments[0];
            Assert.Equal("EVAL", scriptActivity.DisplayName);
            Assert.Equal("EVAL redis.call('set', ARGV[1], ARGV[2])", scriptActivity.GetTagValue(SemanticConventions.AttributeDbStatement));

            VerifyActivityData((Activity)activityProcessor.Invocations[3].Arguments[0], false, connection.GetEndPoints()[0], true);
            VerifyActivityData((Activity)activityProcessor.Invocations[5].Arguments[0], true, connection.GetEndPoints()[0], true);
            VerifyActivityData((Activity)activityProcessor.Invocations[7].Arguments[0], false, connection.GetEndPoints()[0], true);
            VerifySamplingParameters(sampler.LatestSamplingParameters);
        }

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

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            var sampler = new TestSampler();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .SetSampler(sampler)
                .AddRedisInstrumentation(connection, c => c.SetVerboseDatabaseStatements = false)
                .Build())
            {
                var db = connection.GetDatabase();

                bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

                Assert.True(set);

                var redisValue = db.StringGet("key1");

                Assert.True(redisValue.HasValue);
                Assert.Equal(value, redisValue.ToString());
            }

            // Disposing SDK should flush the Redis profiling session immediately.

            Assert.Equal(7, activityProcessor.Invocations.Count);

            VerifyActivityData((Activity)activityProcessor.Invocations[1].Arguments[0], true, connection.GetEndPoints()[0], false);
            VerifyActivityData((Activity)activityProcessor.Invocations[3].Arguments[0], false, connection.GetEndPoints()[0], false);
            VerifySamplingParameters(sampler.LatestSamplingParameters);
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

        [Trait("CategoryName", "RedisIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(RedisEndPointEnvVarName)]
        [InlineData("value1")]
        public void CanEnrichActivityFromCommand(string value)
        {
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = true,
            };
            connectionOptions.EndPoints.Add(RedisEndPoint);

            using var connection = ConnectionMultiplexer.Connect(connectionOptions);

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            var sampler = new TestSampler();
            using (Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .SetSampler(sampler)
                .AddRedisInstrumentation(connection, c => c.Enrich = (activity, command) =>
                {
                    if (command.ElapsedTime < TimeSpan.FromMilliseconds(100))
                    {
                        activity.AddTag("is_fast", true);
                    }
                })
                .Build())
            {
                var db = connection.GetDatabase();

                bool set = db.StringSet("key1", value, TimeSpan.FromSeconds(60));

                Assert.True(set);

                var redisValue = db.StringGet("key1");

                Assert.True(redisValue.HasValue);
                Assert.Equal(value, redisValue.ToString());
            }

            // Disposing SDK should flush the Redis profiling session immediately.

            Assert.Equal(7, activityProcessor.Invocations.Count);

            var setActivity = (Activity)activityProcessor.Invocations[1].Arguments[0];
            Assert.Equal(true, setActivity.GetTagValue("is_fast"));
            var getActivity = (Activity)activityProcessor.Invocations[3].Arguments[0];
            Assert.Equal(true, getActivity.GetTagValue("is_fast"));
        }

        [Fact]
        public void CheckCacheIsFlushedProperly()
        {
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
            };
            connectionOptions.EndPoints.Add("localhost:6379");

            var connection = ConnectionMultiplexer.Connect(connectionOptions);

            using var instrumentation = new StackExchangeRedisCallsInstrumentation(connection, new StackExchangeRedisCallsInstrumentationOptions());
            var profilerFactory = instrumentation.GetProfilerSessionsFactory();

            // start a root level activity
            using Activity rootActivity = new Activity("Parent")
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
                .Start();

            Assert.NotNull(rootActivity.Id);

            // get an initial profiler from root activity
            Activity.Current = rootActivity;
            ProfilingSession profiler0 = profilerFactory();

            // expect different result from synchronous child activity
            ProfilingSession profiler1;
            using (Activity.Current = new Activity("Child-Span-1").SetParentId(rootActivity.Id).Start())
            {
                profiler1 = profilerFactory();
                Assert.NotSame(profiler0, profiler1);
            }

            rootActivity.Stop();
            rootActivity.Dispose();

            instrumentation.Flush();
            Assert.Empty(instrumentation.Cache);
        }

        [Fact]
        public async Task ProfilerSessionsHandleMultipleSpans()
        {
            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
            };
            connectionOptions.EndPoints.Add("localhost:6379");

            var connection = ConnectionMultiplexer.Connect(connectionOptions);

            using var instrumentation = new StackExchangeRedisCallsInstrumentation(connection, new StackExchangeRedisCallsInstrumentationOptions());
            var profilerFactory = instrumentation.GetProfilerSessionsFactory();

            // start a root level activity
            using Activity rootActivity = new Activity("Parent")
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
                .Start();

            Assert.NotNull(rootActivity.Id);

            // get an initial profiler from root activity
            Activity.Current = rootActivity;
            ProfilingSession profiler0 = profilerFactory();

            // expect different result from synchronous child activity
            ProfilingSession profiler1;
            using (Activity.Current = new Activity("Child-Span-1").SetParentId(rootActivity.Id).Start())
            {
                profiler1 = profilerFactory();
                Assert.NotSame(profiler0, profiler1);
            }

            Activity.Current = rootActivity;

            // expect different result from asynchronous child activity
            using (Activity.Current = new Activity("Child-Span-2").SetParentId(rootActivity.Id).Start())
            {
                // lose async context on purpose
                await Task.Delay(100).ConfigureAwait(false);

                ProfilingSession profiler2 = profilerFactory();
                Assert.NotSame(profiler0, profiler2);
                Assert.NotSame(profiler1, profiler2);
            }

            Activity.Current = rootActivity;

            // ensure same result back in root activity
            ProfilingSession profiles3 = profilerFactory();
            Assert.Same(profiler0, profiles3);
        }

        [Fact]
        public void StackExchangeRedis_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddRedisInstrumentation(null));

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            Assert.Throws<NotSupportedException>(() =>
            Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddRedisInstrumentation(null)
                .Build());
        }

        [Fact]
        public void StackExchangeRedis_DependencyInjection_Success()
        {
            bool connectionMultiplexerPickedFromDI = false;
            bool optionsPickedFromDI = false;

            var connectionOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
            };
            connectionOptions.EndPoints.Add("localhost");

            var services = new ServiceCollection();
            services.AddSingleton<IConnectionMultiplexer>((sp) =>
            {
                connectionMultiplexerPickedFromDI = true;
                return ConnectionMultiplexer.Connect(connectionOptions);
            });
            services.Configure<StackExchangeRedisCallsInstrumentationOptions>(options =>
            {
                optionsPickedFromDI = true;
            });
            services.AddOpenTelemetryTracing(builder => builder.AddRedisInstrumentation());

            using var serviceProvider = services.BuildServiceProvider();

            var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

            Assert.True(connectionMultiplexerPickedFromDI);
            Assert.True(optionsPickedFromDI);
        }

        [Fact]
        public void StackExchangeRedis_DependencyInjection_Failure()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetryTracing(builder => builder.AddRedisInstrumentation());

            using var serviceProvider = services.BuildServiceProvider();

            Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<TracerProvider>());
        }

        private static void VerifyActivityData(Activity activity, bool isSet, EndPoint endPoint, bool setCommandKey = false)
        {
            if (isSet)
            {
                Assert.Equal("SETEX", activity.DisplayName);
                if (setCommandKey)
                {
                    Assert.Equal("SETEX key1", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                }
                else
                {
                    Assert.Equal("SETEX", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                }
            }
            else
            {
                Assert.Equal("GET", activity.DisplayName);
                if (setCommandKey)
                {
                    Assert.Equal("GET key1", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                }
                else
                {
                    Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                }
            }

            Assert.Equal(Status.Unset, activity.GetStatus());
            Assert.Equal("redis", activity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal(0, activity.GetTagValue(StackExchangeRedisCallsInstrumentation.RedisDatabaseIndexKeyName));

            if (endPoint is IPEndPoint ipEndPoint)
            {
                Assert.Equal(ipEndPoint.Address.ToString(), activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
                Assert.Equal(ipEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            }
            else if (endPoint is DnsEndPoint dnsEndPoint)
            {
                Assert.Equal(dnsEndPoint.Host, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
                Assert.Equal(dnsEndPoint.Port, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            }
            else
            {
                Assert.Equal(endPoint.ToString(), activity.GetTagValue(SemanticConventions.AttributePeerService));
            }
        }

        private static void VerifySamplingParameters(SamplingParameters samplingParameters)
        {
            Assert.NotNull(samplingParameters.Tags);
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                       && (string)kvp.Value == "redis");
        }
    }
}
