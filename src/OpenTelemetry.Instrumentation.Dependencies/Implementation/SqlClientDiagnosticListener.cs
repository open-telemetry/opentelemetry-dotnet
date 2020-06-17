// <copyright file="SqlClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Data;
using System.Diagnostics;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class SqlClientDiagnosticListener : ListenerHandler
    {
        internal const string SqlDataBeforeExecuteCommand = "System.Data.SqlClient.WriteCommandBefore";
        internal const string SqlMicrosoftBeforeExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandBefore";

        internal const string SqlDataAfterExecuteCommand = "System.Data.SqlClient.WriteCommandAfter";
        internal const string SqlMicrosoftAfterExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandAfter";

        internal const string SqlDataWriteCommandError = "System.Data.SqlClient.WriteCommandError";
        internal const string SqlMicrosoftWriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

        private const string DatabaseStatementTypeSpanAttributeKey = "db.statementType";

        private readonly PropertyFetcher commandFetcher = new PropertyFetcher("Command");
        private readonly PropertyFetcher connectionFetcher = new PropertyFetcher("Connection");
        private readonly PropertyFetcher dataSourceFetcher = new PropertyFetcher("DataSource");
        private readonly PropertyFetcher databaseFetcher = new PropertyFetcher("Database");
        private readonly PropertyFetcher commandTypeFetcher = new PropertyFetcher("CommandType");
        private readonly PropertyFetcher commandTextFetcher = new PropertyFetcher("CommandText");
        private readonly PropertyFetcher exceptionFetcher = new PropertyFetcher("Exception");
        private readonly SqlClientInstrumentationOptions options;
        private readonly ActivitySourceAdapter activitySource;

        public SqlClientDiagnosticListener(string sourceName, SqlClientInstrumentationOptions options, ActivitySourceAdapter activitySource)
            : base(sourceName, null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.activitySource = activitySource;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            switch (name)
            {
                case SqlDataBeforeExecuteCommand:
                case SqlMicrosoftBeforeExecuteCommand:
                    {
                        var command = this.commandFetcher.Fetch(payload);

                        if (command == null)
                        {
                            InstrumentationEventSource.Log.NullPayload($"{nameof(SqlClientDiagnosticListener)}-{name}");
                            return;
                        }

                        var connection = this.connectionFetcher.Fetch(command);
                        var database = this.databaseFetcher.Fetch(connection);

                        // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
                        activity.GetType().GetProperty("Kind").SetValue(activity, ActivityKind.Client);
                        activity.DisplayName = (string)database;

                        this.activitySource.Start(activity);

                        if (activity.IsAllDataRequested)
                        {
                            var dataSource = this.dataSourceFetcher.Fetch(connection);
                            var commandText = this.commandTextFetcher.Fetch(command);

                            activity.AddTag(SpanAttributeConstants.ComponentKey, "sql");
                            activity.AddTag(SpanAttributeConstants.DatabaseTypeKey, "sql");
                            activity.AddTag(SpanAttributeConstants.PeerServiceKey, (string)dataSource);
                            activity.AddTag(SpanAttributeConstants.DatabaseInstanceKey, (string)database);

                            if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                            {
                                activity.AddTag(DatabaseStatementTypeSpanAttributeKey, commandType.ToString());

                                switch (commandType)
                                {
                                    case CommandType.StoredProcedure:
                                        if (this.options.CaptureStoredProcedureCommandName)
                                        {
                                            activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, (string)commandText);
                                        }

                                        break;

                                    case CommandType.Text:
                                        if (this.options.CaptureTextCommandContent)
                                        {
                                            activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, (string)commandText);
                                        }

                                        break;
                                }
                            }
                        }
                    }

                    break;
                case SqlDataAfterExecuteCommand:
                case SqlMicrosoftAfterExecuteCommand:
                    {
                        this.activitySource.Stop(activity);
                    }

                    break;
                case SqlDataWriteCommandError:
                case SqlMicrosoftWriteCommandError:
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                            {
                                Status status = Status.Unknown;
                                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                                activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, exception.Message);
                            }
                            else
                            {
                                InstrumentationEventSource.Log.NullPayload($"{nameof(SqlClientDiagnosticListener)}-{name}");
                            }
                        }

                        this.activitySource.Stop(activity);
                    }

                    break;
            }
        }
    }
}
