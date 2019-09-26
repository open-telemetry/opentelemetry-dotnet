// <copyright file="RedisProfilerEntryToSpanConverter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.StackExchangeRedis.Implementation
{
    using System.Collections.Generic;
    using OpenTelemetry.Trace;
    using StackExchange.Redis.Profiling;

    internal static class RedisProfilerEntryToSpanConverter
    {
        public static ISpan ProfilerCommandToSpan(ITracer tracer, ISpan parentSpan, IProfiledCommand command)
        {
            var name = command.Command; // Example: SET;
            if (string.IsNullOrEmpty(name))
            {
                name = "name";
            }

            var span = tracer.SpanBuilder(name)
                .SetParent(parentSpan)
                .SetSpanKind(SpanKind.Client)
                .SetStartTimestamp(command.CommandCreated)
                .StartSpan();

            if (span.IsRecordingEvents)
            {
                // use https://github.com/opentracing/specification/blob/master/semantic_conventions.md for now

                // Timing example:
                // command.CommandCreated; //2019-01-10 22:18:28Z

                // command.CreationToEnqueued;      // 00:00:32.4571995
                // command.EnqueuedToSending;       // 00:00:00.0352838
                // command.SentToResponse;          // 00:00:00.0060586
                // command.ResponseToCompletion;    // 00:00:00.0002601

                // Total:
                // command.ElapsedTime;             // 00:00:32.4988020

                span.Status = Status.Ok;
                span.SetAttribute("db.type", "redis");
                span.SetAttribute("redis.flags", command.Flags.ToString());

                if (command.Command != null)
                {
                    // Example: "db.statement": SET;
                    span.SetAttribute("db.statement", command.Command);
                }

                if (command.EndPoint != null)
                {
                    // Example: "db.instance": Unspecified/localhost:6379[0]
                    span.SetAttribute("db.instance", string.Concat(command.EndPoint, "[", command.Db, "]"));
                }

                // TODO: deal with the re-transmission
                // command.RetransmissionOf;
                // command.RetransmissionReason;

                var enqueued = command.CommandCreated.Add(command.CreationToEnqueued);
                var send = enqueued.Add(command.EnqueuedToSending);
                var response = send.Add(command.SentToResponse);

                span.AddEvent(Event.Create("Enqueued", enqueued));
                span.AddEvent(Event.Create("Sent", send));
                span.AddEvent(Event.Create("ResponseReceived", response));

                span.End(command.CommandCreated.Add(command.ElapsedTime));
            }

            return span;
        }

        public static void DrainSession(ITracer tracer, ISpan parentSpan, IEnumerable<IProfiledCommand> sessionCommands)
        {
            foreach (var command in sessionCommands)
            {
                ProfilerCommandToSpan(tracer, parentSpan, command);
            }
        }
    }
}
