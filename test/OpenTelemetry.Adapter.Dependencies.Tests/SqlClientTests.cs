// <copyright file="SqlClientTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Adapter.Dependencies.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Adapter.Dependencies.Tests
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
            activity.Start();

            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            using (new SqlClientAdapter(
                tracer,
                new SqlClientAdapterOptions
                {
                    CaptureStoredProcedureCommandName = captureStoredProcedureCommandName,
                    CaptureTextCommandContent = captureTextCommandContent,
                }))
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
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal("master", span.Name);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(StatusCanonicalCode.Ok, span.Status.CanonicalCode);
            Assert.Null(span.Status.Description);

            Assert.Equal("sql", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.ComponentKey).Value as string);
            Assert.Equal("sql", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.DatabaseTypeKey).Value as string);
            Assert.Equal("master", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.DatabaseInstanceKey).Value as string);

            switch (commandType)
            {
                case CommandType.StoredProcedure:
                    if (captureStoredProcedureCommandName)
                    {
                        Assert.Equal(commandText, span.Attributes.FirstOrDefault(i =>
                            i.Key == SpanAttributeConstants.DatabaseStatementKey).Value as string);
                    }
                    else
                    {
                        Assert.Null(span.Attributes.FirstOrDefault(i =>
                            i.Key == SpanAttributeConstants.DatabaseStatementKey).Value as string);
                    }

                    break;

                case CommandType.Text:
                    if (captureTextCommandContent)
                    {
                        Assert.Equal(commandText, span.Attributes.FirstOrDefault(i =>
                            i.Key == SpanAttributeConstants.DatabaseStatementKey).Value as string);
                    }
                    else
                    {
                        Assert.Null(span.Attributes.FirstOrDefault(i =>
                            i.Key == SpanAttributeConstants.DatabaseStatementKey).Value as string);
                    }

                    break;
            }

            Assert.Equal("(localdb)\\MSSQLLocalDB", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.PeerServiceKey).Value as string);

            activity.Stop();
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError)]
        public void SqlClientErrorsAreCollectedSuccessfully(string beforeCommand, string errorCommand)
        {
            var activity = new Activity("Current").AddBaggage("Stuff", "123");
            activity.Start();

            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            using (new SqlClientAdapter(tracer))
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
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            var span = (SpanData)spanProcessor.Invocations[0].Arguments[0];

            Assert.Equal("master", span.Name);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(StatusCanonicalCode.Unknown, span.Status.CanonicalCode);
            Assert.Equal("Boom!", span.Status.Description);

            Assert.Equal("sql", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.ComponentKey).Value as string);
            Assert.Equal("sql", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.DatabaseTypeKey).Value as string);
            Assert.Equal("master", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.DatabaseInstanceKey).Value as string);
            Assert.Equal("SP_GetOrders", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.DatabaseStatementKey).Value as string);
            Assert.Equal("(localdb)\\MSSQLLocalDB", span.Attributes.FirstOrDefault(i =>
                i.Key == SpanAttributeConstants.PeerServiceKey).Value as string);

            activity.Stop();
        }

        private class FakeSqlClientDiagnosticSource : IDisposable
        {
            private readonly DiagnosticListener listener;

            public FakeSqlClientDiagnosticSource()
            {
                this.listener = new DiagnosticListener(SqlClientAdapter.SqlClientDiagnosticListenerName);
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
