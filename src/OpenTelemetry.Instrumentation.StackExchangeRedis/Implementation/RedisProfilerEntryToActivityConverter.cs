// <copyright file="RedisProfilerEntryToActivityConverter.cs" company="OpenTelemetry Authors">
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
using System.Net;
using OpenTelemetry.Trace;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation
{
    internal static class RedisProfilerEntryToActivityConverter
    {
        public static Activity ProfilerCommandToActivity(Activity parentActivity, IProfiledCommand command)
        {
            var name = command.Command; // Example: SET;
            if (string.IsNullOrEmpty(name))
            {
                name = StackExchangeRedisCallsInstrumentation.ActivityName;
            }

            var activity = StackExchangeRedisCallsInstrumentation.ActivitySource.StartActivity(
                name,
                ActivityKind.Client,
                parentActivity?.Context ?? default,
                startTime: command.CommandCreated);

            if (activity == null)
            {
                return null;
            }

            if (activity.IsAllDataRequested == true)
            {
                // see https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md

                // Timing example:
                // command.CommandCreated; //2019-01-10 22:18:28Z

                // command.CreationToEnqueued;      // 00:00:32.4571995
                // command.EnqueuedToSending;       // 00:00:00.0352838
                // command.SentToResponse;          // 00:00:00.0060586
                // command.ResponseToCompletion;    // 00:00:00.0002601

                // Total:
                // command.ElapsedTime;             // 00:00:32.4988020

                activity.SetStatus(Status.Unset);

                activity.SetTag(SemanticConventions.AttributeDbSystem, "redis");
                activity.SetTag(StackExchangeRedisCallsInstrumentation.RedisFlagsKeyName, command.Flags.ToString());

                if (command.Command != null)
                {
                    // Example: "db.statement": SET;
                    activity.SetTag(SemanticConventions.AttributeDbStatement, command.Command);
                }

                if (command.EndPoint != null)
                {
                    if (command.EndPoint is IPEndPoint ipEndPoint)
                    {
                        activity.SetTag(SemanticConventions.AttributeNetPeerIp, ipEndPoint.Address.ToString());
                        activity.SetTag(SemanticConventions.AttributeNetPeerPort, ipEndPoint.Port);
                    }
                    else if (command.EndPoint is DnsEndPoint dnsEndPoint)
                    {
                        activity.SetTag(SemanticConventions.AttributeNetPeerName, dnsEndPoint.Host);
                        activity.SetTag(SemanticConventions.AttributeNetPeerPort, dnsEndPoint.Port);
                    }
                    else
                    {
                        activity.SetTag(SemanticConventions.AttributePeerService, command.EndPoint.ToString());
                    }
                }

                activity.SetTag(StackExchangeRedisCallsInstrumentation.RedisDatabaseIndexKeyName, command.Db);

                // TODO: deal with the re-transmission
                // command.RetransmissionOf;
                // command.RetransmissionReason;

                var enqueued = command.CommandCreated.Add(command.CreationToEnqueued);
                var send = enqueued.Add(command.EnqueuedToSending);
                var response = send.Add(command.SentToResponse);

                activity.AddEvent(new ActivityEvent("Enqueued", enqueued));
                activity.AddEvent(new ActivityEvent("Sent", send));
                activity.AddEvent(new ActivityEvent("ResponseReceived", response));

                activity.SetEndTime(command.CommandCreated + command.ElapsedTime);
            }

            activity.Stop();

            return activity;
        }

        public static void DrainSession(Activity parentActivity, IEnumerable<IProfiledCommand> sessionCommands)
        {
            foreach (var command in sessionCommands)
            {
                ProfilerCommandToActivity(parentActivity, command);
            }
        }
    }
}
