// <copyright file="RedisProfilerEntryToSpanConverter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Trace;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation
{
    internal static class RedisProfilerEntryToSpanConverter
    {
        public static Activity ProfilerCommandToSpan(ActivitySource redisActivitySource, Activity parentSpan, IProfiledCommand command)
        {
            var name = command.Command; // Example: SET;
            if (string.IsNullOrEmpty(name))
            {
                name = "name";
            }

            Activity activity = null;
            if (parentSpan != null)
            {
                activity = redisActivitySource.StartActivity(name, ActivityKind.Client, parentSpan.Context, null, null, command.CommandCreated);
            }
            else
            {
                activity = redisActivitySource.StartActivity(name, ActivityKind.Client, null, null, null, command.CommandCreated);
            }

            if (activity != null && activity.IsAllDataRequested)
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

                // TODO once status is supported.
                // span.Status = Status.Ok;
                activity.AddTag("db.type", "redis");
                activity.AddTag("redis.flags", command.Flags.ToString());

                if (command.Command != null)
                {
                    // Example: "db.statement": SET;
                    activity.AddTag("db.statement", command.Command);
                }

                if (command.EndPoint != null)
                {
                    // Example: "db.instance": Unspecified/localhost:6379[0]
                    activity.AddTag("db.instance", string.Concat(command.EndPoint, "[", command.Db, "]"));
                }

                // TODO: deal with the re-transmission
                // command.RetransmissionOf;
                // command.RetransmissionReason;

                var enqueued = command.CommandCreated.Add(command.CreationToEnqueued);
                var send = enqueued.Add(command.EnqueuedToSending);
                var response = send.Add(command.SentToResponse);

                activity.AddEvent(new ActivityEvent("Enqueued", enqueued));
                activity.AddEvent(new ActivityEvent("Sent", send));
                activity.AddEvent(new ActivityEvent("ResponseReceived", response));

                activity.SetEndTime(command.CommandCreated.Add(command.ElapsedTime));
                activity.Stop();
            }

            return activity;
        }

        public static void DrainSession(ActivitySource redisActivitySource, Activity parentSpan, IEnumerable<IProfiledCommand> sessionCommands)
        {
            foreach (var command in sessionCommands)
            {
                ProfilerCommandToSpan(redisActivitySource, parentSpan, command);
            }
        }
    }
}
