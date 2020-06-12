// <copyright file="SqlClientTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Microsoft.Data.SqlClient;
using Moq;
using OpenTelemetry.Instrumentation.Dependencies.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.Dependencies.Tests
{
    public class SqlClientTests : IDisposable
    {
        private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master";

        private readonly FakeSqlClientDiagnosticSource fakeSqlClientDiagnosticSource;

        public SqlClientTests()
        {
            this.fakeSqlClientDiagnosticSource = new FakeSqlClientDiagnosticSource();
        }

        public void Dispose()
        {
            this.fakeSqlClientDiagnosticSource.Dispose();
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true)]
        public void SqlClientCallsAreCollectedSuccessfully(
            string beforeCommand,
            string afterCommand,
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent)
        {
            var activity = new Activity("Current").AddBaggage("Stuff", "123");

            var spanProcessor = new Mock<ActivityProcessor>();
            using (OpenTelemetrySdk.EnableOpenTelemetry(
                    (builder) => builder.AddSqlClientDependencyInstrumentation(
                        (opt) =>
                        {
                            opt.CaptureTextCommandContent = captureTextCommandContent;
                            opt.CaptureStoredProcedureCommandName = captureStoredProcedureCommandName;
                            })
                    .SetProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))))
            {
                var operationId = Guid.NewGuid();
                var sqlConnection = new SqlConnection(TestConnectionString);
                var sqlCommand = sqlConnection.CreateCommand();
                sqlCommand.CommandType = commandType;
                sqlCommand.CommandText = commandText;

                var beforeExecuteEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Timestamp = (long?)1000000L,
                };

                activity.Start();

                this.fakeSqlClientDiagnosticSource.Write(
                    beforeCommand,
                    beforeExecuteEventData);

                var afterExecuteEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Timestamp = 2000000L,
                };

                this.fakeSqlClientDiagnosticSource.Write(
                    afterCommand,
                    afterExecuteEventData);
                activity.Stop();
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal("master", span.DisplayName);
            Assert.Equal(ActivityKind.Client, span.Kind);

            // TODO: Should Ok status be assigned when no error occurs automatically?
            // Assert.Equal("Ok", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
            Assert.Null(span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusDescriptionKey).Value);

            Assert.Equal("sql", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.ComponentKey).Value);
            Assert.Equal("sql", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseTypeKey).Value);
            Assert.Equal("master", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseInstanceKey).Value);

            switch (commandType)
            {
                case CommandType.StoredProcedure:
                    if (captureStoredProcedureCommandName)
                    {
                        Assert.Equal(commandText, span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
                    }
                    else
                    {
                        Assert.Null(span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
                    }

                    break;

                case CommandType.Text:
                    if (captureTextCommandContent)
                    {
                        Assert.Equal(commandText, span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
                    }
                    else
                    {
                        Assert.Null(span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
                    }

                    break;
            }

            Assert.Equal("(localdb)\\MSSQLLocalDB", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.PeerServiceKey).Value);
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError)]
        public void SqlClientErrorsAreCollectedSuccessfully(string beforeCommand, string errorCommand)
        {
            var activity = new Activity("Current").AddBaggage("Stuff", "123");
            var spanProcessor = new Mock<ActivityProcessor>();

            using (OpenTelemetrySdk.EnableOpenTelemetry(
                (builder) => builder.AddSqlClientDependencyInstrumentation()
                .SetProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))))
            {
                var operationId = Guid.NewGuid();
                var sqlConnection = new SqlConnection(TestConnectionString);
                var sqlCommand = sqlConnection.CreateCommand();
                sqlCommand.CommandText = "SP_GetOrders";
                sqlCommand.CommandType = CommandType.StoredProcedure;

                var beforeExecuteEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Timestamp = (long?)1000000L,
                };

                activity.Start();
                this.fakeSqlClientDiagnosticSource.Write(
                    beforeCommand,
                    beforeExecuteEventData);

                var commandErrorEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Exception = new Exception("Boom!"),
                    Timestamp = 2000000L,
                };

                this.fakeSqlClientDiagnosticSource.Write(
                    errorCommand,
                    commandErrorEventData);

                activity.Stop();
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            var span = (Activity)spanProcessor.Invocations[0].Arguments[0];

            Assert.Equal("master", span.DisplayName);
            Assert.Equal(ActivityKind.Client, span.Kind);

            Assert.Equal("Unknown", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
            Assert.Equal("Boom!", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusDescriptionKey).Value);
            Assert.Equal("sql", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.ComponentKey).Value);
            Assert.Equal("sql", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseTypeKey).Value);
            Assert.Equal("master", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseInstanceKey).Value);
            Assert.Equal("SP_GetOrders", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
            Assert.Equal("(localdb)\\MSSQLLocalDB", span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.PeerServiceKey).Value);
        }

        private class FakeSqlClientDiagnosticSource : IDisposable
        {
            private readonly DiagnosticListener listener;

            public FakeSqlClientDiagnosticSource()
            {
                this.listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);
            }

            public void Write(string name, object value)
            {
                this.listener.Write(name, value);
            }

            public void Dispose()
            {
                this.listener.Dispose();
            }
        }
    }
}
