// <copyright file="StackExchangeRedisCallsCollector.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.StackExchangeRedis
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry.Collector.StackExchangeRedis.Implementation;
    using OpenTelemetry.Trace;
    using StackExchange.Redis.Profiling;

    /// <summary>
    /// Redis calls collector.
    /// </summary>
    public class StackExchangeRedisCallsCollector : IDisposable
    {
        private readonly ITracer tracer;

        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;

        private readonly ProfilingSession defaultSession = new ProfilingSession();
        private readonly ConcurrentDictionary<ISpan, ProfilingSession> cache = new ConcurrentDictionary<ISpan, ProfilingSession>();

        /// <summary>
        /// Initializes a new instance of the <see cref="StackExchangeRedisCallsCollector"/> class.
        /// </summary>
        /// <param name="tracer">Tracer to record traced with.</param>
        public StackExchangeRedisCallsCollector(ITracer tracer)
        {
            this.tracer = tracer;

            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;
            Task.Factory.StartNew(this.DumpEntries, TaskCreationOptions.LongRunning, this.cancellationToken);
        }

        /// <summary>
        /// Returns session for the Redis calls recording.
        /// </summary>
        /// <returns>Session associated with the current span context to record Redis calls.</returns>
        public Func<ProfilingSession> GetProfilerSessionsFactory()
        {
            // This implementation shares session for multiple Redis calls made inside a single parent Span.
            // It cost an additional lookup in concurrent dictionary, but potentially saves an allocation
            // if many calls to Redis were made from the same parent span.
            // Creating a session per Redis call may be more optimal solution here as sampling will not
            // require any locking and can redis the number of buffered sessions significantly.
            return () =>
            {
                var span = this.tracer.CurrentSpan;

                // when there are no spans in current context - BlankSpan will be returned
                // BlankSpan has invalid context. It's OK to use a single profiler session
                // for all invalid context's spans.
                //
                // It would be great to allow to check sampling here, but it is impossible
                // with the current model to start a new trace id here - no way to pass it
                // to the resulting Span.
                if (span == null || !span.Context.IsValid)
                {
                    return this.defaultSession;
                }

                // TODO: As a performance optimization the check for sampling may be implemented here
                // The problem with this approach would be that ActivitySpanId cannot be generated here
                // So if sampler uses ActivitySpanId in algorithm - results would be inconsistent
                var session = this.cache.GetOrAdd(span, (s) => new ProfilingSession(s));
                return session;
            };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
        }

        private void DumpEntries(object state)
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                RedisProfilerEntryToSpanConverter.DrainSession(this.tracer, null, this.defaultSession.FinishProfiling());

                foreach (var entry in this.cache)
                {
                    var span = entry.Key;
                    ProfilingSession session;
                    if (span.HasEnded)
                    {
                        this.cache.TryRemove(span, out session);
                    }
                    else
                    {
                        this.cache.TryGetValue(span, out session);
                    }

                    if (session != null)
                    {
                        RedisProfilerEntryToSpanConverter.DrainSession(this.tracer, span, session.FinishProfiling());
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}
