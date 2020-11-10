// <copyright file="JaegerExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Exporter.Jaeger.Tests.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class JaegerExporterTests
    {
        [Fact]
        public void JaegerExporter_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddJaegerExporter());
        }

        [Fact]
        public void JaegerTraceExporter_ctor_NullServiceNameAllowed()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions
            {
                ServiceName = null,
            });
            Assert.NotNull(jaegerTraceExporter);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_UpdatesServiceName()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            process.ServiceName = "TestService";

            jaegerTraceExporter.SetResource(Resource.Empty);

            Assert.Equal("TestService", process.ServiceName);

            jaegerTraceExporter.SetResource(Resources.Resources.CreateServiceResource("MyService"));

            Assert.Equal("MyService", process.ServiceName);

            jaegerTraceExporter.SetResource(Resources.Resources.CreateServiceResource("MyService", serviceNamespace: "MyNamespace"));

            Assert.Equal("MyNamespace.MyService", process.ServiceName);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_CreatesTags()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            jaegerTraceExporter.SetResource(new Resource(new Dictionary<string, object>
            {
                ["Tag"] = "value",
            }));

            Assert.NotNull(process.Tags);
            Assert.Single(process.Tags);
            Assert.Equal("value", process.Tags["Tag"].VStr);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_CombinesTags()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            process.Tags = new Dictionary<string, JaegerTag> { ["Tag1"] = new KeyValuePair<string, object>("Tag1", "value1").ToJaegerTag() };

            jaegerTraceExporter.SetResource(new Resource(new Dictionary<string, object>
            {
                ["Tag2"] = "value2",
            }));

            Assert.NotNull(process.Tags);
            Assert.Equal(2, process.Tags.Count);
            Assert.Equal("value1", process.Tags["Tag1"].VStr);
            Assert.Equal("value2", process.Tags["Tag2"].VStr);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_IgnoreLibraryResources()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            jaegerTraceExporter.SetResource(new Resource(new Dictionary<string, object>
            {
                [Resource.LibraryNameKey] = "libname",
                [Resource.LibraryVersionKey] = "libversion",
            }));

            Assert.Null(process.Tags);
        }

        [Fact]
        public void JaegerTraceExporter_BuildBatchesToTransmit_DefaultBatch()
        {
            // Arrange
            using var jaegerExporter = new JaegerExporter(new JaegerExporterOptions { ServiceName = "TestService" });
            jaegerExporter.SetResource(Resource.Empty);

            // Act
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());

            var batches = jaegerExporter.CurrentBatches.Values;

            // Assert
            Assert.Single(batches);
            Assert.Equal("TestService", batches.First().Process.ServiceName);
            Assert.Equal(3, batches.First().Count);
        }

        [Fact]
        public void JaegerTraceExporter_BuildBatchesToTransmit_MultipleBatches()
        {
            // Arrange
            using var jaegerExporter = new JaegerExporter(new JaegerExporterOptions { ServiceName = "TestService" });
            jaegerExporter.SetResource(Resource.Empty);

            // Act
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(
                CreateTestJaegerSpan(
                    additionalAttributes: new Dictionary<string, object>
                    {
                        ["peer.service"] = "MySQL",
                    }));
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());

            var batches = jaegerExporter.CurrentBatches.Values;

            // Assert
            Assert.Equal(2, batches.Count());

            var primaryBatch = batches.Where(b => b.Process.ServiceName == "TestService");
            Assert.Single(primaryBatch);
            Assert.Equal(2, primaryBatch.First().Count);

            var mySQLBatch = batches.Where(b => b.Process.ServiceName == "MySQL");
            Assert.Single(mySQLBatch);
            Assert.Equal(1, mySQLBatch.First().Count);
        }

        [Fact]
        public void JaegerTraceExporter_BuildBatchesToTransmit_FlushedBatch()
        {
            // Arrange
            using var jaegerExporter = new JaegerExporter(new JaegerExporterOptions { ServiceName = "TestService", MaxPayloadSizeInBytes = 1500 });
            jaegerExporter.SetResource(Resource.Empty);

            // Act
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());

            var batches = jaegerExporter.CurrentBatches.Values;

            // Assert
            Assert.Single(batches);
            Assert.Equal("TestService", batches.First().Process.ServiceName);
            Assert.Equal(1, batches.First().Count);
        }

        internal static JaegerSpan CreateTestJaegerSpan(
            bool setAttributes = true,
            Dictionary<string, object> additionalAttributes = null,
            bool addEvents = true,
            bool addLinks = true,
            Resource resource = null,
            ActivityKind kind = ActivityKind.Client)
        {
            return JaegerActivityConversionTest
                .CreateTestActivity(
                    setAttributes, additionalAttributes, addEvents, addLinks, resource, kind)
                .ToJaegerSpan();
        }
    }
}
