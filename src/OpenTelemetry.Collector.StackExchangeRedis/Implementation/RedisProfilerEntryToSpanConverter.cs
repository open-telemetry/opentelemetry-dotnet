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
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using StackExchange.Redis.Profiling;

    internal static class RedisProfilerEntryToSpanConverter
    {
        public static void DrainSession(ISpan parentSpan, IEnumerable<IProfiledCommand> sessionCommands, ISampler sampler, ICollection<ISpanData> spans)
        {
            var parentContext = parentSpan?.Context ?? SpanContext.Invalid;

            foreach (var command in sessionCommands)
            {
                string name = command.Command; // Example: SET;
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

        internal static bool ShouldSample(ISpanContext parentContext, string name, ISampler sampler, out ISpanContext context, out ISpanId parentSpanId)
        {
            var traceId = TraceId.Invalid;
            var tracestate = Tracestate.Empty;
            parentSpanId = SpanId.Invalid;
            var parentOptions = TraceOptions.Default;

            if (parentContext.IsValid)
            {
                traceId = parentContext.TraceId;
                parentSpanId = parentContext.SpanId;
                parentOptions = parentContext.TraceOptions;
            }
            else
            {
                traceId = TraceId.FromBytes(Guid.NewGuid().ToByteArray());
            }

            var result = parentOptions.IsSampled;
            bool hasRemoteParent = false;
            var spanId = SpanId.FromBytes(Guid.NewGuid().ToByteArray(), 8);
            var traceOptions = TraceOptions.Default;

            if (sampler != null)
            {
                var builder = TraceOptions.Builder(parentContext.TraceOptions);
                result = sampler.ShouldSample(parentContext, hasRemoteParent, traceId, spanId, name, null);
                builder = builder.SetIsSampled(result);
                traceOptions = builder.Build();
            }

            context = SpanContext.Create(traceId, spanId, traceOptions, parentContext.Tracestate);

            return result;
        }

        internal static ISpanData ProfiledCommandToSpanData(ISpanContext context, string name, ISpanId parentSpanId, IProfiledCommand command)
        {
            var hasRemoteParent = false;

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
            Timestamp startTimestamp = Timestamp.FromMillis(new DateTimeOffset(command.CommandCreated).ToUnixTimeMilliseconds());

            var timestamp = new DateTimeOffset(command.CommandCreated).Add(command.CreationToEnqueued);
            var annotations = TimedEvents<IAnnotation>.Create(
                new List<ITimedEvent<IAnnotation>>()
                {
                    TimedEvent<IAnnotation>.Create(Timestamp.FromMillis(timestamp.ToUnixTimeMilliseconds()), Annotation.FromDescription("Enqueued")),
                    TimedEvent<IAnnotation>.Create(Timestamp.FromMillis((timestamp = timestamp.Add(command.EnqueuedToSending)).ToUnixTimeMilliseconds()), Annotation.FromDescription("Sent")),
                    TimedEvent<IAnnotation>.Create(Timestamp.FromMillis((timestamp = timestamp.Add(command.SentToResponse)).ToUnixTimeMilliseconds()), Annotation.FromDescription("ResponseRecieved")),
                },
                droppedEventsCount: 0);

            Timestamp endTimestamp = Timestamp.FromMillis(new DateTimeOffset(command.CommandCreated.Add(command.ElapsedTime)).ToUnixTimeMilliseconds());

            // TODO: deal with the re-transmission
            // command.RetransmissionOf;
            // command.RetransmissionReason;

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

            ITimedEvents<IMessageEvent> messageOrNetworkEvents = null;
            ILinks links = null;
            int? childSpanCount = 0;

            // TODO: this is strange that IProfiledCommand doesn't give the result
            Status status = Status.Ok;
            SpanKind kind = SpanKind.Client;

            return SpanData.Create(context, parentSpanId, hasRemoteParent, name, startTimestamp, attributes, annotations, messageOrNetworkEvents, links, childSpanCount, status, kind, endTimestamp);
        }
    }
}
