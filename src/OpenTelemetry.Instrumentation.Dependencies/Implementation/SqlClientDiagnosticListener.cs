﻿// <copyright file="SqlClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    internal class SqlClientDiagnosticListener : ListenerHandler
    {
        internal const string ActivitySourceName = "System.Data.SqlClient";
        internal const string ActivityName = ActivitySourceName + ".Execute";

        internal const string SqlDataBeforeExecuteCommand = "System.Data.SqlClient.WriteCommandBefore";
        internal const string SqlMicrosoftBeforeExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandBefore";

        internal const string SqlDataAfterExecuteCommand = "System.Data.SqlClient.WriteCommandAfter";
        internal const string SqlMicrosoftAfterExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandAfter";

        internal const string SqlDataWriteCommandError = "System.Data.SqlClient.WriteCommandError";
        internal const string SqlMicrosoftWriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

        internal const string MicrosoftSqlServerDatabaseSystemName = "mssql";

        private static readonly Version Version = typeof(SqlClientDiagnosticListener).Assembly.GetName().Version;
#pragma warning disable SA1202 // Elements should be ordered by access <- In this case, Version MUST come before SqlClientActivitySource otherwise null ref exception is thrown.
        internal static readonly ActivitySource SqlClientActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
#pragma warning restore SA1202 // Elements should be ordered by access

        private readonly PropertyFetcher commandFetcher = new PropertyFetcher("Command");
        private readonly PropertyFetcher connectionFetcher = new PropertyFetcher("Connection");
        private readonly PropertyFetcher dataSourceFetcher = new PropertyFetcher("DataSource");
        private readonly PropertyFetcher databaseFetcher = new PropertyFetcher("Database");
        private readonly PropertyFetcher commandTypeFetcher = new PropertyFetcher("CommandType");
        private readonly PropertyFetcher commandTextFetcher = new PropertyFetcher("CommandText");
        private readonly PropertyFetcher exceptionFetcher = new PropertyFetcher("Exception");
        private readonly SqlClientInstrumentationOptions options;

        public SqlClientDiagnosticListener(string sourceName, SqlClientInstrumentationOptions options)
            : base(sourceName)
        {
            this.options = options ?? new SqlClientInstrumentationOptions();
        }

        public override bool SupportsNullActivity => true;

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
                            InstrumentationEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener), name);
                            return;
                        }

                        var connection = this.connectionFetcher.Fetch(command);
                        var database = this.databaseFetcher.Fetch(connection);

                        // SqlClient does not create an Activity. So the activity coming in here will be null or the root span.
                        activity = SqlClientActivitySource.StartActivity(ActivityName, ActivityKind.Client);
                        if (activity == null)
                        {
                            return;
                        }

                        // TODO: Avoid the reflection hack once .NET ships new Activity with Kind settable.
                        activity.GetType().GetProperty("Kind").SetValue(activity, ActivityKind.Client);
                        activity.DisplayName = (string)database;

                        if (activity.IsAllDataRequested)
                        {
                            var dataSource = this.dataSourceFetcher.Fetch(connection);
                            var commandText = this.commandTextFetcher.Fetch(command);

                            activity.AddTag(SpanAttributeConstants.ComponentKey, "sql");
                            activity.AddTag(SpanAttributeConstants.DatabaseSystemKey, MicrosoftSqlServerDatabaseSystemName);
                            activity.AddTag(SpanAttributeConstants.DatabaseNameKey, (string)database);

                            this.options.AddConnectionLevelDetailsToActivity((string)dataSource, activity);

                            if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                            {
                                switch (commandType)
                                {
                                    case CommandType.StoredProcedure:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.StoredProcedure));
                                        if (this.options.CaptureStoredProcedureCommandName)
                                        {
                                            activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, (string)commandText);
                                        }

                                        break;

                                    case CommandType.Text:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.Text));
                                        if (this.options.CaptureTextCommandContent)
                                        {
                                            activity.AddTag(SpanAttributeConstants.DatabaseStatementKey, (string)commandText);
                                        }

                                        break;

                                    case CommandType.TableDirect:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.TableDirect));
                                        break;
                                }
                            }
                        }
                    }

                    break;
                case SqlDataAfterExecuteCommand:
                case SqlMicrosoftAfterExecuteCommand:
                    {
                        if (activity == null)
                        {
                            InstrumentationEventSource.Log.NullActivity(name);
                            return;
                        }

                        if (activity.Source != SqlClientActivitySource)
                        {
                            return;
                        }

                        try
                        {
                            if (activity.IsAllDataRequested)
                            {
                                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(StatusCanonicalCode.Ok));
                            }
                        }
                        finally
                        {
                            activity.Stop();
                        }
                    }

                    break;
                case SqlDataWriteCommandError:
                case SqlMicrosoftWriteCommandError:
                    {
                        if (activity == null)
                        {
                            InstrumentationEventSource.Log.NullActivity(name);
                            return;
                        }

                        if (activity.Source != SqlClientActivitySource)
                        {
                            return;
                        }

                        try
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
                                    InstrumentationEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener), name);
                                }
                            }
                        }
                        finally
                        {
                            activity.Stop();
                        }
                    }

                    break;
            }
        }
    }
}
