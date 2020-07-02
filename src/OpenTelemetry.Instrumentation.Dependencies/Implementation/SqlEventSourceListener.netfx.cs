// <copyright file="SqlEventSourceListener.netfx.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Reflection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    /// <summary>
    /// .NET Framework SqlClient doesn't emit DiagnosticSource events.
    /// We hook into its EventSource if it is available:
    /// See: <a href="https://github.com/microsoft/referencesource/blob/3b1eaf5203992df69de44c783a3eda37d3d4cd10/System.Data/System/Data/Common/SqlEventSource.cs#L29">reference source</a>.
    /// </summary>
    internal class SqlEventSourceListener : EventListener
    {
        internal const string ActivitySourceName = "SqlClient";
        internal const string ActivityName = ActivitySourceName + ".Execute";

        private static readonly Version Version = typeof(SqlEventSourceListener).Assembly.GetName().Version;
        private static readonly ActivitySource SqlClientActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
        private static readonly EventSource SqlEventSource = (EventSource)typeof(SqlConnection).Assembly.GetType("System.Data.SqlEventSource")?.GetField("Log", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

        private readonly SqlClientInstrumentationOptions options;

        public SqlEventSourceListener(SqlClientInstrumentationOptions options = null)
        {
            this.options = options ?? new SqlClientInstrumentationOptions();

            if (SqlEventSource != null)
            {
                this.EnableEvents(SqlEventSource, EventLevel.Informational);
            }
        }

        public override void Dispose()
        {
            if (SqlEventSource != null)
            {
                this.DisableEvents(SqlEventSource);
            }

            base.Dispose();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 1)
            {
                // BeginExecuteEventId

                var activity = SqlClientActivitySource.StartActivity(ActivityName, ActivityKind.Client);
                if (activity == null)
                {
                    return;
                }

                activity.DisplayName = (string)eventData.Payload[2];

                if (activity.IsAllDataRequested)
                {
                    activity.AddTag(SpanAttributeConstants.ComponentKey, "sql");

                    activity.AddTag(SpanAttributeConstants.DatabaseTypeKey, "sql");
                    activity.AddTag(SpanAttributeConstants.PeerServiceKey, (string)eventData.Payload[1]);
                    activity.AddTag(SpanAttributeConstants.DatabaseInstanceKey, (string)eventData.Payload[2]);

                    if (string.IsNullOrEmpty((string)eventData.Payload[3]))
                    {
                        activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, "Text");
                    }
                    else
                    {
                        activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, "StoredProcedure");
                        if (this.options.CaptureStoredProcedureCommandName)
                        {
                            activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, (string)eventData.Payload[3]);
                        }
                    }
                }
            }
            else if (eventData.EventId == 2)
            {
                // EndExecuteEventId

                var activity = Activity.Current;
                if (activity == null || activity.Source != SqlClientActivitySource)
                {
                    return;
                }

                try
                {
                    if (activity.IsAllDataRequested)
                    {
                        if (((int)eventData.Payload[1] & 0x01) == 0x00)
                        {
                            activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(StatusCanonicalCode.Unknown));
                            activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, $"SqlExceptionNumber {eventData.Payload[2]} thrown.");
                        }
                        else
                        {
                            activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(StatusCanonicalCode.Ok));
                        }
                    }
                }
                finally
                {
                    activity.Stop();
                }
            }
        }
    }
}
#endif
