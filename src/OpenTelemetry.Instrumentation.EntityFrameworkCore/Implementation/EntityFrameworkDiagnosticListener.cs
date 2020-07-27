// <copyright file="EntityFrameworkDiagnosticListener.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation
{
    internal sealed class EntityFrameworkDiagnosticListener : ListenerHandler
    {
        internal const string DiagnosticSourceName = "Microsoft.EntityFrameworkCore";

        internal const string ActivitySourceName = "OpenTelemetry.EntityFrameworkCore";
        internal const string ActivityName = ActivitySourceName + ".Execute";

        // TODO: get this value from payload
        internal const string DatabaseSystemName = "mssql";

        internal static readonly ActivitySource SqlClientActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

        private static readonly Version Version = typeof(EntityFrameworkDiagnosticListener).Assembly.GetName().Version;

        private readonly PropertyFetcher commandFetcher = new PropertyFetcher("Command");
        private readonly PropertyFetcher connectionFetcher = new PropertyFetcher("Connection");
        private readonly PropertyFetcher dataSourceFetcher = new PropertyFetcher("DataSource");
        private readonly PropertyFetcher databaseFetcher = new PropertyFetcher("Database");
        private readonly PropertyFetcher commandTypeFetcher = new PropertyFetcher("CommandType");
        private readonly PropertyFetcher commandTextFetcher = new PropertyFetcher("CommandText");
        private readonly PropertyFetcher exceptionFetcher = new PropertyFetcher("Exception");

        private readonly EntityFrameworkInstrumentationOptions options;

        public EntityFrameworkDiagnosticListener(string sourceName, EntityFrameworkInstrumentationOptions options)
            : base(sourceName)
        {
            this.options = options;
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            switch (name)
            {
                case "Microsoft.EntityFrameworkCore.Database.Command.CommandCreated":
                    {
                        activity = SqlClientActivitySource.StartActivity(ActivityName, ActivityKind.Client);
                        if (activity == null)
                        {
                            // There is no listener or it decided not to sample the current request.
                            return;
                        }

                        var command = this.commandFetcher.Fetch(payload);
                        if (command == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
                            activity.Stop();
                            return;
                        }


                        if (activity.IsAllDataRequested)
                        {
                            var connection = this.connectionFetcher.Fetch(command);
                            var database = (string)this.databaseFetcher.Fetch(connection);
                            var dataSource = this.dataSourceFetcher.Fetch(connection);

                            activity.DisplayName = database;
                            activity.AddTag(SemanticConventions.AttributeDbSystem, DatabaseSystemName);
                            activity.AddTag(SemanticConventions.AttributeDbName, database);

                            // TODO:
                            // this.options.AddConnectionLevelDetailsToActivity((string)dataSource, activity);
                        }
                    }

                    break;

                case "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting":
                    {
                        if (activity == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                            return;
                        }

                        if (activity.Source != SqlClientActivitySource)
                        {
                            return;
                        }

                        if (activity.IsAllDataRequested)
                        {
                            var command = this.commandFetcher.Fetch(payload);

                            if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                            {
                                var commandText = this.commandTextFetcher.Fetch(command);
                                switch (commandType)
                                {
                                    case CommandType.StoredProcedure:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.StoredProcedure));
                                        if (this.options != null && this.options.SetStoredProcedureCommandName)
                                        {
                                            activity.AddTag(SemanticConventions.AttributeDbStatement, (string)commandText);
                                        }

                                        break;

                                    case CommandType.Text:
                                        activity.AddTag(SpanAttributeConstants.DatabaseStatementTypeKey, nameof(CommandType.Text));
                                        if (this.options != null && this.options.SetTextCommandContent)
                                        {
                                            activity.AddTag(SemanticConventions.AttributeDbStatement, (string)commandText);
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

                case "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted":
                    {
                        if (activity == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
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

                case "Microsoft.EntityFrameworkCore.Database.Command.CommandError":
                    {
                        if (activity == null)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
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
                                    EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
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
