// <copyright file="JaegerTraceExporterTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class JaegerTraceExporterTests
    {
        [Fact]
        public void Constructor_EmptyServiceName_ThrowsArgumentNullException()
        {
            // Arrange
            var options = new JaegerExporterOptions();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new JaegerTraceExporter(options));
            Assert.Equal("ServiceName", exception.ParamName);
        }
        
        [Fact]
        public void Constructor_ValidOptions_ReturnsInstance()
        {
            // Arrange
            var options = new JaegerExporterOptions {ServiceName = "test_service"};

            // Act
            var exporter = new JaegerTraceExporter(options);
            
            // Assert
            Assert.NotNull(exporter);
        }
    }
}
