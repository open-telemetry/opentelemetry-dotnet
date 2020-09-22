// <copyright file="ZipkinActivityExporterRemoteEndpointTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests.Implementation
{
    public class ZipkinActivityExporterRemoteEndpointTests
    {
        private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new ZipkinEndpoint("TestService");

        [Fact]
        public void GenerateActivity_RemoteEndpointOmittedByDefault()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity();

            // Act & Assert
            var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
        }

        [Fact]
        public void GenerateActivity_RemoteEndpointResolution()
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["net.peer.name"] = "RemoteServiceName",
                });

            // Act & Assert
            var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
            Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
        }

        [Theory]
        [MemberData(nameof(RemoteEndpointPriorityTestCase.GetTestCases), MemberType = typeof(RemoteEndpointPriorityTestCase))]
        public void GenerateActivity_RemoteEndpointResolutionPriority(RemoteEndpointPriorityTestCase testCase)
        {
            // Arrange
            var activity = ZipkinExporterTests.CreateTestActivity(additionalAttributes: testCase.RemoteEndpointAttributes);

            // Act & Assert
            var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
            Assert.Equal(testCase.ExpectedResult, zipkinSpan.RemoteEndpoint.ServiceName);
        }

        public class RemoteEndpointPriorityTestCase
        {
            public string Name { get; set; }

            public string ExpectedResult { get; set; }

            public Dictionary<string, object> RemoteEndpointAttributes { get; set; }

            public static IEnumerable<object[]> GetTestCases()
            {
                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "Highest priority name = net.peer.name",
                        ExpectedResult = "RemoteServiceName",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            ["http.host"] = "DiscardedRemoteServiceName",
                            ["net.peer.name"] = "RemoteServiceName",
                            ["peer.hostname"] = "DiscardedRemoteServiceName",
                        },
                    },
                };

                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "Highest priority name = SemanticConventions.AttributePeerService",
                        ExpectedResult = "RemoteServiceName",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            [SemanticConventions.AttributePeerService] = "RemoteServiceName",
                            ["http.host"] = "DiscardedRemoteServiceName",
                            ["net.peer.name"] = "DiscardedRemoteServiceName",
                            ["net.peer.port"] = "1234",
                            ["peer.hostname"] = "DiscardedRemoteServiceName",
                        },
                    },
                };

                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "Only has net.peer.name and net.peer.port",
                        ExpectedResult = "RemoteServiceName:1234",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            ["net.peer.name"] = "RemoteServiceName",
                            ["net.peer.port"] = "1234",
                        },
                    },
                };

                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "net.peer.port is an int",
                        ExpectedResult = "RemoteServiceName:1234",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            ["net.peer.name"] = "RemoteServiceName",
                            ["net.peer.port"] = 1234,
                        },
                    },
                };

                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "Has net.peer.name and net.peer.port",
                        ExpectedResult = "RemoteServiceName:1234",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            ["http.host"] = "DiscardedRemoteServiceName",
                            ["net.peer.name"] = "RemoteServiceName",
                            ["net.peer.port"] = "1234",
                            ["peer.hostname"] = "DiscardedRemoteServiceName",
                        },
                    },
                };

                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "Has net.peer.ip and net.peer.port",
                        ExpectedResult = "1.2.3.4:1234",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            ["http.host"] = "DiscardedRemoteServiceName",
                            ["net.peer.ip"] = "1.2.3.4",
                            ["net.peer.port"] = "1234",
                            ["peer.hostname"] = "DiscardedRemoteServiceName",
                        },
                    },
                };

                yield return new object[]
                {
                    new RemoteEndpointPriorityTestCase
                    {
                        Name = "Has net.peer.name, net.peer.ip, and net.peer.port",
                        ExpectedResult = "RemoteServiceName:1234",
                        RemoteEndpointAttributes = new Dictionary<string, object>
                        {
                            ["http.host"] = "DiscardedRemoteServiceName",
                            ["net.peer.name"] = "RemoteServiceName",
                            ["net.peer.ip"] = "1.2.3.4",
                            ["net.peer.port"] = "1234",
                            ["peer.hostname"] = "DiscardedRemoteServiceName",
                        },
                    },
                };
            }

            public override string ToString()
            {
                return this.Name;
            }
        }
    }
}
