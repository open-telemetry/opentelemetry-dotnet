// <copyright file="EventSourceBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Benchmarks
{
    [MemoryDiagnoser]
    public class EventSourceBenchmarks
    {
        internal static OpenTelemetryEventListener Listener;
        private static readonly Func<Activity, string> TraceIdGetter = CreateFieldGetter<Activity, string>("_traceId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Func<Activity, string> SpanIdGetter = CreateFieldGetter<Activity, string>("_spanId", BindingFlags.Instance | BindingFlags.NonPublic);

        [Params(false, true)]
        public bool Listen { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (this.Listen)
            {
                Listener = new OpenTelemetryEventListener();
            }
        }

        [Benchmark]
        public void EventWithId()
        {
            Activity activity = new Activity("TestActivity");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
            activity.Stop();

            OpenTelemetryBenchmarksEventSource.Log.ActivityStarted(activity.OperationName, activity.Id);
        }

        [Benchmark]
        public void EventWithValues()
        {
            Activity activity = new Activity("TestActivity");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
            activity.Stop();

            OpenTelemetryBenchmarksEventSource.Log.ActivityStartedSimple(activity.OperationName, TraceIdGetter(activity), SpanIdGetter(activity));
        }

        private static Func<TClass, TField> CreateFieldGetter<TClass, TField>(string fieldName, BindingFlags flags)
            where TClass : class
        {
            FieldInfo field = typeof(TClass).GetField(fieldName, flags);
            if (field == null)
            {
                throw new InvalidOperationException($"Field [{fieldName}] could not be found.");
            }

            string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(TField), new[] { typeof(TClass) }, true);
            ILGenerator generator = getterMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, field);
            generator.Emit(OpCodes.Ret);
            return (Func<TClass, TField>)getterMethod.CreateDelegate(typeof(Func<TClass, TField>));
        }

        [EventSource(Name = "OpenTelemetry-Benchmarks")]
        internal class OpenTelemetryBenchmarksEventSource : EventSource
        {
            public static OpenTelemetryBenchmarksEventSource Log = new OpenTelemetryBenchmarksEventSource();

            [Event(1, Message = "Activity started. OperationName = '{0}', Id = '{1}'.", Level = EventLevel.Verbose)]
            public void ActivityStarted(string operationName, string id)
            {
                this.WriteEvent(1, operationName, id);
            }

            [Event(2, Message = "Activity started. OperationName = '{0}', TraceId = '{1}', SpanId = '{2}'.", Level = EventLevel.Verbose)]
            public void ActivityStartedSimple(string operationName, string traceId, string spanId)
            {
                this.WriteEvent(2, operationName, traceId, spanId);
            }
        }

        internal class OpenTelemetryEventListener : EventListener
        {
            private readonly List<EventSource> eventSources = new List<EventSource>();

            public override void Dispose()
            {
                foreach (EventSource eventSource in this.eventSources)
                {
                    this.DisableEvents(eventSource);
                }

                base.Dispose();
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource?.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.eventSources.Add(eventSource);
                    this.EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)(-1));
                }

                base.OnEventSourceCreated(eventSource);
            }

            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
            }
        }
    }
}
