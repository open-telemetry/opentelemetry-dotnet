// <copyright file="ActivityEventAttachingLogProcessor.cs" company="OpenTelemetry Authors">
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

#if NET461 || NETSTANDARD2_0
using System;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Logs
{
    internal class ActivityEventAttachingLogProcessor : BaseProcessor<LogRecord>
    {
        private static readonly Action<object, State> ProcessScopeRef = ProcessScope;

        private readonly ActivityEventAttachingLogProcessorOptions options;

        public ActivityEventAttachingLogProcessor(ActivityEventAttachingLogProcessorOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnEnd(LogRecord data)
        {
            Activity activity = Activity.Current;

            if (activity?.IsAllDataRequested == true)
            {
                var tags = new ActivityTagsCollection
                {
                    { nameof(data.CategoryName), data.CategoryName },
                    { nameof(data.LogLevel), data.LogLevel },
                };

                if (data.EventId != 0)
                {
                    tags[nameof(data.EventId)] = data.EventId;
                }

                var activityEvent = new ActivityEvent("log", data.Timestamp, tags);

                if (this.options.IncludeScopes)
                {
                    data.ForEachScope(
                        ProcessScopeRef,
                        new State
                        {
                            Tags = tags,
                            Processor = this,
                        });
                }

                if (data.State != null)
                {
                    if (this.options.IncludeState)
                    {
                        try
                        {
                            this.options.StateConverter?.Invoke(tags, data.State);
                        }
                        catch (Exception ex)
                        {
                            OpenTelemetrySdkEventSource.Log.LogProcessorException($"Processing state of type [{data.State.GetType().FullName}]", ex);
                        }
                    }
                    else
                    {
                        tags["Message"] = data.State.ToString();
                    }
                }

                activity.AddEvent(activityEvent);

                if (data.Exception != null)
                {
                    activity.RecordException(data.Exception);
                }
            }
        }

        private static void ProcessScope(object scope, State state)
        {
            if (scope != null)
            {
                try
                {
                    state.Processor.options.ScopeConverter?.Invoke(state.Tags, state.Index++, scope);
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.LogProcessorException($"Processing scope of type [{scope.GetType().FullName}]", ex);
                }
            }
        }

        private class State
        {
            public ActivityTagsCollection Tags;
            public int Index;
            public ActivityEventAttachingLogProcessor Processor;
        }
    }
}
#endif
