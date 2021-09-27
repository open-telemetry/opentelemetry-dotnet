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

            var reader = new BaseExportingMetricReader(exporter);
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMetricReader(reader)
                .Build();

            switch (mode)
            {
                case ExportModes.Push:
                    Assert.True(reader.Collect());
                    Assert.True(meterProvider.ForceFlush());
                    break;
                case ExportModes.Pull:
                    Assert.False(reader.Collect());
                    Assert.False(meterProvider.ForceFlush());
                    Assert.True((exporter as IPullMetricExporter).Collect(-1));
                    break;
                case ExportModes.Pull | ExportModes.Push:
                    Assert.True(reader.Collect());
                    Assert.True(meterProvider.ForceFlush());
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
        private class PullOnlyMetricExporter : BaseExporter<Metric>, IPullMetricExporter
        {
            private Func<int, bool> funcCollect;

            public Func<int, bool> Collect
            {
                get => this.funcCollect;
                set { this.funcCollect = value; }
            }

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
