// <copyright file="JaegerUdpBatcherTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerUdpBatcherTests
    {
        [Fact]
        public async Task JaegerUdpBatcherTests_BuildBatchesToTransmit_DefaultBatch()
        {
            // Arrange
            var options = new JaegerExporterOptions { ServiceName = "TestService", MaxFlushInterval = TimeSpan.FromHours(1) };

            var jaegerUdpBatcher = new JaegerUdpBatcher(options);

            // Act
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);

            var batches = jaegerUdpBatcher.CurrentBatches.Values;

            // Assert
            Assert.Single(batches);
            Assert.Equal("TestService", batches.First().Process.ServiceName);
            Assert.Equal(3, batches.First().SpanMessages.Count());
        }

        [Fact]
        public async Task JaegerUdpBatcherTests_BuildBatchesToTransmit_MultipleBatches()
        {
            // Arrange
            var options = new JaegerExporterOptions { ServiceName = "TestService", MaxFlushInterval = TimeSpan.FromHours(1) };

            var jaegerUdpBatcher = new JaegerUdpBatcher(options);

            // Act
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(
                JaegerSpanConverterTest.CreateTestSpan(
                    additionalAttributes: new Dictionary<string, object>
                    {
                        ["peer.service"] = "MySQL",
                    }),
                CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);

            var batches = jaegerUdpBatcher.CurrentBatches.Values;

            // Assert
            Assert.Equal(2, batches.Count());

            var PrimaryBatch = batches.Where(b => b.Process.ServiceName == "TestService");
            Assert.Single(PrimaryBatch);
            Assert.Equal(2, PrimaryBatch.First().SpanMessages.Count());

            var MySQLBatch = batches.Where(b => b.Process.ServiceName == "MySQL");
            Assert.Single(MySQLBatch);
            Assert.Single(MySQLBatch.First().SpanMessages);
        }

        [Fact]
        public async Task JaegerUdpBatcherTests_BuildBatchesToTransmit_FlushedBatch()
        {
            // Arrange
            var options = new JaegerExporterOptions { ServiceName = "TestService", MaxFlushInterval = TimeSpan.FromHours(1), MaxPacketSize = 750 };

            var jaegerUdpBatcher = new JaegerUdpBatcher(options);

            // Act
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);
            var flushCount = await jaegerUdpBatcher.AppendAsync(JaegerSpanConverterTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);

            var batches = jaegerUdpBatcher.CurrentBatches.Values;

            // Assert
            Assert.Equal(2, flushCount);
            Assert.Single(batches);
            Assert.Equal("TestService", batches.First().Process.ServiceName);
            Assert.Single(batches.First().SpanMessages);
        }

        [Fact]
        public async Task JaegerUdpBatcher_IntegrationTest()
        {
            var validJaegerThriftPayload = Convert.FromBase64String(JaegerThriftIntegrationTest.TestPayloadBase64);

            var memoryTransport = new InMemoryTransport();

            using var jaegerUdpBatcher = new JaegerUdpBatcher(
                new JaegerExporterOptions { MaxFlushInterval = TimeSpan.FromHours(1) },
                memoryTransport);
            jaegerUdpBatcher.Process = JaegerThriftIntegrationTest.TestProcess;

            await jaegerUdpBatcher.AppendAsync(JaegerThriftIntegrationTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);

            await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(Convert.ToBase64String(validJaegerThriftPayload), Convert.ToBase64String(memoryTransport.ToArray()));

            memoryTransport.Reset();

            await jaegerUdpBatcher.AppendAsync(JaegerThriftIntegrationTest.CreateTestSpan(), CancellationToken.None).ConfigureAwait(false);

            await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            // SeqNo is the second byte.
            validJaegerThriftPayload[2]++;

            Assert.Equal(Convert.ToBase64String(validJaegerThriftPayload), Convert.ToBase64String(memoryTransport.ToArray()));
        }
    }
}
