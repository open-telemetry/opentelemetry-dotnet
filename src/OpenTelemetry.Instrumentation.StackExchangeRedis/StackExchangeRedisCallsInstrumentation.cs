// <copyright file="StackExchangeRedisCallsInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis
{
    /// <summary>
    /// Redis calls instrumentation.
    /// </summary>
    internal class StackExchangeRedisCallsInstrumentation : IDisposable
    {
        internal const string RedisDatabaseIndexKeyName = "db.redis.database_index";
        internal const string RedisFlagsKeyName = "db.redis.flags";
        internal const string ActivitySourceName = "OpenTelemetry.StackExchange.Redis";
        internal const string ActivityName = ActivitySourceName + ".Execute";
        internal static readonly Version Version = typeof(StackExchangeRedisCallsInstrumentation).Assembly.GetName().Version;
        internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

        internal readonly ConcurrentDictionary<(ActivityTraceId TraceId, ActivitySpanId SpanId), (Activity Activity, ProfilingSession Session)> Cache
            = new ConcurrentDictionary<(ActivityTraceId, ActivitySpanId), (Activity, ProfilingSession)>();

        private readonly StackExchangeRedisCallsInstrumentationOptions options;
        private readonly EventWaitHandle stopHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly Thread drainThread;

        private readonly ProfilingSession defaultSession = new ProfilingSession();

        /// <summary>
        /// Initializes a new instance of the <see cref="StackExchangeRedisCallsInstrumentation"/> class.
        /// </summary>
        /// <param name="connection"><see cref="IConnectionMultiplexer"/> to instrument.</param>
        /// <param name="options">Configuration options for redis instrumentation.</param>
        public StackExchangeRedisCallsInstrumentation(IConnectionMultiplexer connection, StackExchangeRedisCallsInstrumentationOptions options)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            this.options = options ?? new StackExchangeRedisCallsInstrumentationOptions();

            this.drainThread = new Thread(this.DrainEntries)
            {
                Name = "OpenTelemetry.Redis",
            };
            this.drainThread.Start();

            connection.RegisterProfiler(this.GetProfilerSessionsFactory());
        }

        /// <summary>
        /// Returns session for the Redis calls recording.
        /// </summary>
        /// <returns>Session associated with the current span context to record Redis calls.</returns>
        public Func<ProfilingSession> GetProfilerSessionsFactory()
        {
            return () =>
            {
                if (this.stopHandle.WaitOne(0))
                {
                    return null;
                }

                Activity parent = Activity.Current;

                // If no parent use the default session.
                if (parent == null || parent.IdFormat != ActivityIdFormat.W3C)
                {
                    return this.defaultSession;
                }

                // Try to reuse a session for all activities created under the same TraceId+SpanId.
                var cacheKey = (parent.TraceId, parent.SpanId);
                if (!this.Cache.TryGetValue(cacheKey, out var session))
                {
                    session = (parent, new ProfilingSession());
                    this.Cache.TryAdd(cacheKey, session);
                }

                return session.Session;
            };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.stopHandle.Set();
            this.drainThread.Join();

            this.Flush();

            this.stopHandle.Dispose();
        }

        internal void Flush()
        {
            RedisProfilerEntryToActivityConverter.DrainSession(null, this.defaultSession.FinishProfiling());

            foreach (var entry in this.Cache)
            {
                var parent = entry.Value.Activity;
                if (parent.Duration == TimeSpan.Zero)
                {
                    // Activity is still running, don't drain.
                    continue;
                }

                ProfilingSession session = entry.Value.Session;
                RedisProfilerEntryToActivityConverter.DrainSession(parent, session.FinishProfiling());
                this.Cache.TryRemove((entry.Key.TraceId, entry.Key.SpanId), out _);
            }
        }

        private void DrainEntries(object state)
        {
            while (true)
            {
                if (this.stopHandle.WaitOne(this.options.FlushInterval))
                {
                    break;
                }

                this.Flush();
            }
        }
    }
}
