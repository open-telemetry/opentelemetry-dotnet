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

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation
{
    internal class SqlClientDiagnosticListener : ListenerHandler
    {
        public const string ActivitySourceName = "OpenTelemetry.SqlClient";
        public const string ActivityName = ActivitySourceName + ".Execute";

        public const string CommandCustomPropertyName = "OTel.SqlHandler.Command";

        public const string SqlDataBeforeExecuteCommand = "System.Data.SqlClient.WriteCommandBefore";
        public const string SqlMicrosoftBeforeExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandBefore";

        public const string SqlDataAfterExecuteCommand = "System.Data.SqlClient.WriteCommandAfter";
        public const string SqlMicrosoftAfterExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandAfter";

        public const string SqlDataWriteCommandError = "System.Data.SqlClient.WriteCommandError";
        public const string SqlMicrosoftWriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

        public const string MicrosoftSqlServerDatabaseSystemName = "mssql";

        private static readonly Version Version = typeof(SqlClientDiagnosticListener).Assembly.GetName().Version;
#pragma warning disable SA1202 // Elements should be ordered by access <- In this case, Version MUST come before SqlClientActivitySource otherwise null ref exception is thrown.
        internal static readonly ActivitySource SqlClientActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
#pragma warning restore SA1202 // Elements should be ordered by access

        private readonly PropertyFetcher<object> commandFetcher = new PropertyFetcher<object>("Command");
        private readonly PropertyFetcher<object> connectionFetcher = new PropertyFetcher<object>("Connection");
        private readonly PropertyFetcher<object> dataSourceFetcher = new PropertyFetcher<object>("DataSource");
        private readonly PropertyFetcher<object> databaseFetcher = new PropertyFetcher<object>("Database");
        private readonly PropertyFetcher<CommandType> commandTypeFetcher = new PropertyFetcher<CommandType>("CommandType");
        private readonly PropertyFetcher<object> commandTextFetcher = new PropertyFetcher<object>("CommandText");
        private readonly PropertyFetcher<Exception> exceptionFetcher = new PropertyFetcher<Exception>("Exception");
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
                        // SqlClient does not create an Activity. So the activity coming in here will be null or the root span.
                        activity = SqlClientActivitySource.StartActivity(ActivityName, ActivityKind.Client);
                        if (activity == null)
                        {
                            // There is no listener or it decided not to sample the current request.
                            return;
                        }

                        var command = this.commandFetcher.Fetch(payload);
                        if (command == null)
                        {
                            SqlClientInstrumentationEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener), name);
                            activity.Stop();
                            return;
                        }

                        if (activity.IsAllDataRequested)
                        {
                            var connection = this.connectionFetcher.Fetch(command);
                            var database = this.databaseFetcher.Fetch(connection);

                            activity.DisplayName = (string)database;
                            try
                            {
                                this.options.Enrich?.Invoke(activity, "OnCustom", command);
                            }
                            catch (Exception ex)
                            {
                                SqlClientInstrumentationEventSource.Log.EnrichmentException(ex);
                            }

                            var dataSource = this.dataSourceFetcher.Fetch(connection);
                            var commandText = this.commandTextFetcher.Fetch(command);

                            activity.SetTag(SemanticConventions.AttributeDbSystem, MicrosoftSqlServerDatabaseSystemName);
                            activity.SetTag(SemanticConventions.AttributeDbName, (string)database);

                            this.options.AddConnectionLevelDetailsToActivity((string)dataSource, activity);

                            if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                            {
                                switch (commandType)
                                {
                                    case CommandType.StoredProcedure:
                                        activity.SetTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.StoredProcedure));
                                        if (this.options.SetStoredProcedureCommandName)
                                        {
                                            activity.SetTag(SemanticConventions.AttributeDbStatement, (string)commandText);
                                        }

                                        break;

                                    case CommandType.Text:
                                        activity.SetTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.Text));
                                        if (this.options.SetTextCommandContent)
                                        {
                                            activity.SetTag(SemanticConventions.AttributeDbStatement, (string)commandText);
                                        }

                                        break;

                                    case CommandType.TableDirect:
                                        activity.SetTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.TableDirect));
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
                            SqlClientInstrumentationEventSource.Log.NullActivity(name);
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
                                activity.SetStatus(Status.Ok);
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
                            SqlClientInstrumentationEventSource.Log.NullActivity(name);
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
                                    activity.SetStatus(Status.Unknown.WithDescription(exception.Message));
                                }
                                else
                                {
                                    SqlClientInstrumentationEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener), name);
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
