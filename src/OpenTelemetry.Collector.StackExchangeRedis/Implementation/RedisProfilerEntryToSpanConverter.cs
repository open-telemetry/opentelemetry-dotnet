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
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Common;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using StackExchange.Redis.Profiling;

    internal static class RedisProfilerEntryToSpanConverter
    {
        public static void DrainSession(ISpan parentSpan, IEnumerable<IProfiledCommand> sessionCommands, ISampler sampler, ICollection<SpanData> spans)
        {
            var parentContext = parentSpan?.Context ?? SpanContext.Blank;

            foreach (var command in sessionCommands)
            {
                var name = command.Command; // Example: SET;
                if (string.IsNullOrEmpty(name))
                {
                    name = "name";
                }

                if (ShouldSample(parentContext, name, sampler, out var context, out var parentSpanId))
                {
                    var sd = ProfiledCommandToSpanData(context, name, parentSpanId, command);
                    spans.Add(sd);
                }
            }
        }

        internal static bool ShouldSample(SpanContext parentContext, string name, ISampler sampler, out SpanContext context, out ActivitySpanId parentSpanId)
        {
            ActivityTraceId traceId = default;
            var tracestate = Tracestate.Empty;
            parentSpanId = default;
            var parentOptions = ActivityTraceFlags.None;

            if (parentContext.IsValid)
            {
                traceId = parentContext.TraceId;
                parentSpanId = parentContext.SpanId;
                parentOptions = parentContext.TraceOptions;
            }
            else
            {
                traceId = ActivityTraceId.CreateRandom();
            }

            var result = (parentOptions & ActivityTraceFlags.Recorded) != 0;
            var spanId = ActivitySpanId.CreateRandom();
            var traceOptions = ActivityTraceFlags.None;

            if (sampler != null)
            {
                traceOptions = parentContext.TraceOptions;
                result = sampler.ShouldSample(parentContext, traceId, spanId, name, null);
                if (result)
                {
                    traceOptions |= ActivityTraceFlags.Recorded;
                }
            }

            context = SpanContext.Create(traceId, spanId, traceOptions, parentContext.Tracestate);

            return result;
        }

        internal static SpanData ProfiledCommandToSpanData(SpanContext context, string name, ActivitySpanId parentSpanId, IProfiledCommand command)
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

            // TODO: make timestamp with the better precision
            var startTimestamp = Timestamp.FromMillis(new DateTimeOffset(command.CommandCreated).ToUnixTimeMilliseconds());

            var timestamp = new DateTimeOffset(command.CommandCreated).Add(command.CreationToEnqueued);
            var events = TimedEvents<IEvent>.Create(
                new List<ITimedEvent<IEvent>>()
                {
                    TimedEvent<IEvent>.Create(Timestamp.FromMillis(timestamp.ToUnixTimeMilliseconds()), Event.Create("Enqueued")),
                    TimedEvent<IEvent>.Create(Timestamp.FromMillis((timestamp = timestamp.Add(command.EnqueuedToSending)).ToUnixTimeMilliseconds()), Event.Create("Sent")),
                    TimedEvent<IEvent>.Create(Timestamp.FromMillis(timestamp.Add(command.SentToResponse).ToUnixTimeMilliseconds()), Event.Create("ResponseRecieved")),
                },
                droppedEventsCount: 0);

            var endTimestamp = Timestamp.FromMillis(new DateTimeOffset(command.CommandCreated.Add(command.ElapsedTime)).ToUnixTimeMilliseconds());

            // TODO: deal with the re-transmission
            // command.RetransmissionOf;
            // command.RetransmissionReason;

            // TODO: determine what to do with Resource in this context
            var resource = Resource.Empty;

            var attributesMap = new Dictionary<string, IAttributeValue>()
            {
                // TODO: pre-allocate constant attribute and reuse
                { "db.type", AttributeValue.StringAttributeValue("redis") },

                // Example: "redis.flags": None, DemandMaster
                { "redis.flags", AttributeValue.StringAttributeValue(command.Flags.ToString()) },
            };

            if (command.Command != null)
            {
                // Example: "db.statement": SET;
                attributesMap.Add("db.statement", AttributeValue.StringAttributeValue(command.Command));
            }

            if (command.EndPoint != null)
            {
                // Example: "db.instance": Unspecified/localhost:6379[0]
                attributesMap.Add("db.instance", AttributeValue.StringAttributeValue(command.EndPoint.ToString() + "[" + command.Db + "]"));
            }

            var attributes = Attributes.Create(attributesMap, 0);

            ILinks links = null;
            int? childSpanCount = 0;

            // TODO: this is strange that IProfiledCommand doesn't give the result
            var status = Status.Ok;
            var kind = SpanKind.Client;

            return SpanData.Create(context, parentSpanId, resource, name, startTimestamp, attributes, events, links, childSpanCount, status, kind, endTimestamp);
        }
    }
}
