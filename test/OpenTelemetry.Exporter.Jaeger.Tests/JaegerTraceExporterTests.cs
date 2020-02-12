﻿// <copyright file="ProcessTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerTraceExporterTests
    {
        [Fact]
        public void JaegerTraceExporter_ctor_NullServiceNameAllowed()
        {
            using (var jaegerTraceExporter = new JaegerTraceExporter(new JaegerExporterOptions
            {
                ServiceName = null,
            }))
            {
                Assert.NotNull(jaegerTraceExporter);
            }
        }

        [Fact]
        public void JaegerTraceExporter_ApplyLibraryResource_UpdatesServiceName()
        {
            using (var jaegerTraceExporter = new JaegerTraceExporter(new JaegerExporterOptions()))
            {
                var process = jaegerTraceExporter.JaegerAgentUdpBatcher.Process;

                process.ServiceName = "TestService";

                jaegerTraceExporter.ApplyLibraryResource(Resource.Empty);

                Assert.Equal("TestService", process.ServiceName);

                jaegerTraceExporter.ApplyLibraryResource(Resources.Resources.CreateServiceResource("MyService"));

                Assert.Equal("MyService", process.ServiceName);

                jaegerTraceExporter.ApplyLibraryResource(Resources.Resources.CreateServiceResource("MyService", serviceNamespace: "MyNamespace"));

                Assert.Equal("MyNamespace.MyService", process.ServiceName);
            }
        }

        [Fact]
        public void JaegerTraceExporter_ApplyLibraryResource_CreatesTags()
        {
            using (var jaegerTraceExporter = new JaegerTraceExporter(new JaegerExporterOptions()))
            {
                var process = jaegerTraceExporter.JaegerAgentUdpBatcher.Process;

                jaegerTraceExporter.ApplyLibraryResource(new Resource(new Dictionary<string, object>
                {
                    ["Tag"] = "value",
                }));

                Assert.NotNull(process.Tags);
                Assert.Single(process.Tags);
                Assert.Equal("value", process.Tags["Tag"].VStr);
            }
        }

        [Fact]
        public void JaegerTraceExporter_ApplyLibraryResource_CombinesTags()
        {
            using (var jaegerTraceExporter = new JaegerTraceExporter(new JaegerExporterOptions()))
            {
                var process = jaegerTraceExporter.JaegerAgentUdpBatcher.Process;

                process.Tags = new Dictionary<string, JaegerTag> { ["Tag1"] = new KeyValuePair<string, object>("Tag1", "value1").ToJaegerTag() };

                jaegerTraceExporter.ApplyLibraryResource(new Resource(new Dictionary<string, object>
                {
                    ["Tag2"] = "value2",
                }));

                Assert.NotNull(process.Tags);
                Assert.Equal(2, process.Tags.Count);
                Assert.Equal("value1", process.Tags["Tag1"].VStr);
                Assert.Equal("value2", process.Tags["Tag2"].VStr);
            }
        }

        [Fact]
        public void JaegerTraceExporter_ApplyLibraryResource_IgnoreLibraryResources()
        {
            using (var jaegerTraceExporter = new JaegerTraceExporter(new JaegerExporterOptions()))
            {
                var process = jaegerTraceExporter.JaegerAgentUdpBatcher.Process;

                jaegerTraceExporter.ApplyLibraryResource(new Resource(new Dictionary<string, object>
                {
                    [Resource.LibraryNameKey] = "libname",
                    [Resource.LibraryVersionKey] = "libversion",
                }));

                Assert.Null(process.Tags);
            }
        }
    }
}
