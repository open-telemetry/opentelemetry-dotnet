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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerUdpBatcherTests
    {
        [Fact]
        public async Task JaegerUdpBatcher_IntegrationTest()
        {
            var validJaegerThriftPayload = Convert.FromBase64String(JaegerThriftIntegrationTest.TestPayloadBase64);

            var memoryTransport = new InMemoryTransport();

            using (var jaegerUdpBatcher = new JaegerUdpBatcher(new JaegerExporterOptions(), memoryTransport))
            {
                jaegerUdpBatcher.Process = JaegerThriftIntegrationTest.TestProcess;

                await jaegerUdpBatcher.AppendAsync(JaegerThriftIntegrationTest.CreateTestSpan().ToJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

                await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);

                Assert.Equal(Convert.ToBase64String(validJaegerThriftPayload), Convert.ToBase64String(memoryTransport.FlushToArray()));

                await jaegerUdpBatcher.AppendAsync(JaegerThriftIntegrationTest.CreateTestSpan().ToJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

                await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);

                // SeqNo is the second byte.
                validJaegerThriftPayload[2]++;

                Assert.Equal(Convert.ToBase64String(validJaegerThriftPayload), Convert.ToBase64String(memoryTransport.FlushToArray()));
            }
        }
    }
}
