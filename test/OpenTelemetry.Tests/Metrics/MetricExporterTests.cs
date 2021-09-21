// <copyright file="MetricExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricExporterTests
    {
        [Theory]
        [InlineData(ExportModes.Push)]
        [InlineData(ExportModes.Pull)]
        [InlineData(ExportModes.Pull | ExportModes.Push)]
        public void FlushMetricExporterTest(ExportModes mode)
        {
            BaseExporter<Metric> exporter = null;

            switch (mode)
            {
                case ExportModes.Push:
                    exporter = new PushOnlyMetricExporter();
                    break;
                case ExportModes.Pull:
                    exporter = new PullOnlyMetricExporter();
                    break;
                case ExportModes.Pull | ExportModes.Push:
                    exporter = new PushPullMetricExporter();
                    break;
            }

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMetricReader(new BaseExportingMetricReader(exporter))
                .Build();

            var result = meterProvider.ForceFlush();

            switch (mode)
            {
                case ExportModes.Push:
                    Assert.True(result);
                    break;
                case ExportModes.Pull:
                    Assert.False(result);
                    break;
                case ExportModes.Pull | ExportModes.Push:
                    Assert.True(result);
                    break;
            }
        }

        [ExportModes(ExportModes.Push)]
        private class PushOnlyMetricExporter : BaseExporter<Metric>
        {
            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }

        [ExportModes(ExportModes.Pull)]
        private class PullOnlyMetricExporter : BaseExporter<Metric>
        {
            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }

        [ExportModes(ExportModes.Pull | ExportModes.Push)]
        private class PushPullMetricExporter : BaseExporter<Metric>
        {
            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }
    }
}
