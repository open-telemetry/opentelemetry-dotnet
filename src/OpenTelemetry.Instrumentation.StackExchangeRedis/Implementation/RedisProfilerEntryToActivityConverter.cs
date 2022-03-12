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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using OpenTelemetry.Trace;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation
{
    internal static class RedisProfilerEntryToActivityConverter
    {
        private static readonly Lazy<Func<object, (string, string)>> MessageDataGetter = new(() =>
        {
            var redisAssembly = typeof(IProfiledCommand).Assembly;
            Type profiledCommandType = redisAssembly.GetType("StackExchange.Redis.Profiling.ProfiledCommand");
            Type messageType = redisAssembly.GetType("StackExchange.Redis.Message");
            Type scriptMessageType = redisAssembly.GetType("StackExchange.Redis.RedisDatabase+ScriptEvalMessage");

            var messageDelegate = CreateFieldGetter<object>(profiledCommandType, "Message", BindingFlags.NonPublic | BindingFlags.Instance);
            var scriptDelegate = CreateFieldGetter<string>(scriptMessageType, "script", BindingFlags.NonPublic | BindingFlags.Instance);
            var commandAndKeyFetcher = new PropertyFetcher<string>("CommandAndKey");

            if (messageDelegate == null)
            {
                return new Func<object, (string, string)>(source => (null, null));
            }

            return new Func<object, (string, string)>(source =>
            {
                if (source == null)
                {
                    return (null, null);
                }

                var message = messageDelegate(source);
                if (message == null)
                {
                    return (null, null);
                }

                string script = null;
                if (message.GetType() == scriptMessageType)
                {
                    script = scriptDelegate.Invoke(message);
                }

                if (commandAndKeyFetcher.TryFetch(message, out var value))
                {
                    return (value, script);
                }

                return (null, script);
            });
        });

        public static Activity ProfilerCommandToActivity(Activity parentActivity, IProfiledCommand command, StackExchangeRedisCallsInstrumentationOptions options)
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
                StackExchangeRedisCallsInstrumentation.CreationTags,
                startTime: command.CommandCreated);

            if (activity == null)
            {
                return null;
            }

            activity.SetEndTime(command.CommandCreated + command.ElapsedTime);

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

                activity.SetTag(StackExchangeRedisCallsInstrumentation.RedisFlagsKeyName, command.Flags.ToString());

                if (options.SetVerboseDatabaseStatements)
                {
                    var (commandAndKey, script) = MessageDataGetter.Value.Invoke(command);

                    if (!string.IsNullOrEmpty(commandAndKey) && !string.IsNullOrEmpty(script))
                    {
                        activity.SetTag(SemanticConventions.AttributeDbStatement, commandAndKey + " " + script);
                    }
                    else if (!string.IsNullOrEmpty(commandAndKey))
                    {
                        activity.SetTag(SemanticConventions.AttributeDbStatement, commandAndKey);
                    }
                    else if (command.Command != null)
                    {
                        // Example: "db.statement": SET;
                        activity.SetTag(SemanticConventions.AttributeDbStatement, command.Command);
                    }
                }
                else if (command.Command != null)
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

                options.Enrich?.Invoke(activity, command);
            }

            activity.Stop();

            return activity;
        }

        public static void DrainSession(Activity parentActivity, IEnumerable<IProfiledCommand> sessionCommands, StackExchangeRedisCallsInstrumentationOptions options)
        {
            foreach (var command in sessionCommands)
            {
                ProfilerCommandToActivity(parentActivity, command, options);
            }
        }

        /// <summary>
        /// Creates getter for a field defined in private or internal type
        /// repesented with classType variable.
        /// </summary>
        private static Func<object, TField> CreateFieldGetter<TField>(Type classType, string fieldName, BindingFlags flags)
        {
            FieldInfo field = classType.GetField(fieldName, flags);
            if (field != null)
            {
                string methodName = classType.FullName + ".get_" + field.Name;
                DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(TField), new[] { typeof(object) }, true);
                ILGenerator generator = getterMethod.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Castclass, classType);
                generator.Emit(OpCodes.Ldfld, field);
                generator.Emit(OpCodes.Ret);

                return (Func<object, TField>)getterMethod.CreateDelegate(typeof(Func<object, TField>));
            }

            return null;
        }
    }
}
