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
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation
{
    /// <summary>
    /// On .NET Framework, neither System.Data.SqlClient nor Microsoft.Data.SqlClient emit DiagnosticSource events.
    /// Instead they use EventSource:
    /// For System.Data.SqlClient see: <a href="https://github.com/microsoft/referencesource/blob/3b1eaf5203992df69de44c783a3eda37d3d4cd10/System.Data/System/Data/Common/SqlEventSource.cs#L29">reference source</a>.
    /// For Microsoft.Data.SqlClient see: <a href="https://github.com/dotnet/SqlClient/blob/ac8bb3f9132e6c104dc3e307fe2d569daed0776f/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlClientEventSource.cs#L15">SqlClientEventSource</a>.
    ///
    /// We hook into these event sources and process their BeginExecute/EndExecute events.
    /// </summary>
    /// <remarks>
    /// Note that before version 2.0.0, Microsoft.Data.SqlClient used "Microsoft-AdoNet-SystemData"
    /// EventSource (same as System.Data.SqlClient), but since 2.0.0 has switched to "Microsoft.Data.SqlClient.EventSource".
    ///
    /// Due to the limitation of the "Microsoft-AdoNet-SystemData", it is not possible to capture sql statement text
    /// for CommandType.Text when using that EventSource. It only reports text for CommandType.StoredProcedure.
    ///
    /// "Microsoft.Data.SqlClient.EventSource" doesn't have that issue.
    /// </remarks>
    internal class SqlEventSourceListener : EventListener
    {
        internal const string AdoNetEventSourceName = "Microsoft-AdoNet-SystemData";
        internal const string MdsEventSourceName = "Microsoft.Data.SqlClient.EventSource";

        internal const int BeginExecuteEventId = 1;
        internal const int EndExecuteEventId = 2;

        private readonly SqlClientInstrumentationOptions options;
        private EventSource adoNetEventSource;
        private EventSource mdsEventSource;

        public SqlEventSourceListener(SqlClientInstrumentationOptions options = null)
        {
            this.options = options ?? new SqlClientInstrumentationOptions();
        }

        public override void Dispose()
        {
            if (this.adoNetEventSource != null)
            {
                this.DisableEvents(this.adoNetEventSource);
            }

            if (this.mdsEventSource != null)
            {
                this.DisableEvents(this.mdsEventSource);
            }

            base.Dispose();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource?.Name.StartsWith(AdoNetEventSourceName, StringComparison.Ordinal) == true)
            {
                this.adoNetEventSource = eventSource;
                this.EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)1);
            }
            else if (eventSource?.Name.StartsWith(MdsEventSourceName, StringComparison.Ordinal) == true)
            {
                this.mdsEventSource = eventSource;
                this.EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)1);
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            try
            {
                if (eventData.EventId == BeginExecuteEventId)
                {
                    this.OnBeginExecute(eventData);
                }
                else if (eventData.EventId == EndExecuteEventId)
                {
                    this.OnEndExecute(eventData);
                }
            }
            catch (Exception exc)
            {
                SqlClientInstrumentationEventSource.Log.UnknownErrorProcessingEvent(nameof(SqlEventSourceListener), nameof(this.OnEventWritten), exc);
            }
        }

        private void OnBeginExecute(EventWrittenEventArgs eventData)
        {
            /*
               Expected payload:
                [0] -> ObjectId
                [1] -> DataSource
                [2] -> Database
                [3] -> CommandText

                Note:
                - For "Microsoft-AdoNet-SystemData": [3] CommandText = (CommandType == CommandType.StoredProcedure ? CommandText : string.Empty;
                - For "Microsoft.Data.SqlClient.EventSource": [3] CommandText = sqlCommand.CommandText (so it is set for all command types).
             */

            if ((eventData?.Payload?.Count ?? 0) < 4)
            {
                SqlClientInstrumentationEventSource.Log.InvalidPayload(nameof(SqlEventSourceListener), nameof(this.OnBeginExecute));
                return;
            }

            var activity = SqlActivitySourceHelper.ActivitySource.StartActivity(SqlActivitySourceHelper.ActivityName, ActivityKind.Client);
            if (activity == null)
            {
                // There is no listener or it decided not to sample the current request.
                return;
            }

            string databaseName = (string)eventData.Payload[2];

            activity.DisplayName = databaseName;

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(SemanticConventions.AttributeDbSystem, SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName);
                activity.SetTag(SemanticConventions.AttributeDbName, databaseName);

                this.options.AddConnectionLevelDetailsToActivity((string)eventData.Payload[1], activity);

                string commandText = (string)eventData.Payload[3];
                if (!string.IsNullOrEmpty(commandText) && this.options.SetDbStatement)
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, commandText);
                }
            }
        }

        private void OnEndExecute(EventWrittenEventArgs eventData)
        {
            /*
               Expected payload:
                [0] -> ObjectId
                [1] -> CompositeState bitmask (0b001 -> successFlag, 0b010 -> isSqlExceptionFlag , 0b100 -> synchronousFlag)
                [2] -> SqlExceptionNumber
             */

            if ((eventData?.Payload?.Count ?? 0) < 3)
            {
                SqlClientInstrumentationEventSource.Log.InvalidPayload(nameof(SqlEventSourceListener), nameof(this.OnEndExecute));
                return;
            }

            var activity = Activity.Current;
            if (activity?.Source != SqlActivitySourceHelper.ActivitySource)
            {
                return;
            }

            try
            {
                if (activity.IsAllDataRequested)
                {
                    int compositeState = (int)eventData.Payload[1];
                    if ((compositeState & 0b001) == 0b001)
                    {
                        activity.SetStatus(Status.Unset);
                    }
                    else if ((compositeState & 0b010) == 0b010)
                    {
                        var errorText = $"SqlExceptionNumber {eventData.Payload[2]} thrown.";
                        activity.SetStatus(Status.Error.WithDescription(errorText));
                    }
                    else
                    {
                        activity.SetStatus(Status.Error.WithDescription("Unknown Sql failure."));
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
#endif
