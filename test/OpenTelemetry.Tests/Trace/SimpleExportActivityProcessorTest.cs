// <copyright file="SimpleExportActivityProcessorTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Tests.Shared;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Trace
{
    public class SimpleExportActivityProcessorTest
    {
        [Fact]
        public void CheckNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleExportActivityProcessor(null));
        }

        [Fact]
        public void CheckExportedOnEnd()
        {
            using var exporter = new TestActivityExporter();
            using var processor = new SimpleExportActivityProcessor(exporter);

            processor.OnEnd(new Activity("start1"));
            Assert.Single(exporter.Exported);

            processor.OnEnd(new Activity("start2"));
            Assert.Equal(2, exporter.Exported.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckForceFlushExport(int timeout)
        {
            using var exporter = new TestActivityExporter();
            using var processor = new SimpleExportActivityProcessor(exporter);

            processor.OnEnd(new Activity("start1"));
            processor.OnEnd(new Activity("start2"));

            // checking before force flush
            Assert.Equal(2, exporter.Exported.Count);

            // forcing flush
            processor.ForceFlush(timeout);
            Assert.Equal(2, exporter.Exported.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckShutdownExport(int timeout)
        {
            using var exporter = new TestActivityExporter();
            using var processor = new SimpleExportActivityProcessor(exporter);

            processor.OnEnd(new Activity("start"));

            // checking before shutdown
            Assert.Single(exporter.Exported);

            processor.Shutdown(timeout);
            Assert.Single(exporter.Exported);
        }
    }
}
