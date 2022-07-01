// <copyright file="PeriodicExportingMetricReaderHelperTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Internal.Tests
{
    public class PeriodicExportingMetricReaderHelperTests : IDisposable
    {
        public PeriodicExportingMetricReaderHelperTests()
        {
            ClearEnvVars();
        }

        public void Dispose()
        {
            ClearEnvVars();
        }

        [Fact]
        public void CreatePeriodicExportingMetricReader_Defaults()
        {
            var reader = CreatePeriodicExportingMetricReader();

            Assert.Equal(60000, reader.ExportIntervalMilliseconds);
            Assert.Equal(30000, reader.ExportTimeoutMilliseconds);
            Assert.Equal(MetricReaderTemporalityPreference.Cumulative, reader.TemporalityPreference);
        }

        [Fact]
        public void CreatePeriodicExportingMetricReader_TemporalityPreference_FromOptions()
        {
            var value = MetricReaderTemporalityPreference.Delta;
            var reader = CreatePeriodicExportingMetricReader(new()
            {
                TemporalityPreference = value,
            });

            Assert.Equal(value, reader.TemporalityPreference);
        }

        [Fact]
        public void CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromOptions()
        {
            Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderHelper.OTelMetricExportIntervalEnvVarKey, "88888"); // should be ignored, as value set via options has higher priority
            var value = 123;
            var reader = CreatePeriodicExportingMetricReader(new()
            {
                PeriodicExportingMetricReaderOptions = new()
                {
                    ExportIntervalMilliseconds = value,
                },
            });

            Assert.Equal(value, reader.ExportIntervalMilliseconds);
        }

        [Fact]
        public void CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromOptions()
        {
            Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderHelper.OTelMetricExportTimeoutEnvVarKey, "99999"); // should be ignored, as value set via options has higher priority
            var value = 456;
            var reader = CreatePeriodicExportingMetricReader(new()
            {
                PeriodicExportingMetricReaderOptions = new()
                {
                    ExportTimeoutMilliseconds = value,
                },
            });

            Assert.Equal(value, reader.ExportTimeoutMilliseconds);
        }

        [Fact]
        public void CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar()
        {
            var value = 789;
            Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderHelper.OTelMetricExportIntervalEnvVarKey, value.ToString());
            var reader = CreatePeriodicExportingMetricReader();

            Assert.Equal(value, reader.ExportIntervalMilliseconds);
        }

        [Fact]
        public void CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar()
        {
            var value = 246;
            Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderHelper.OTelMetricExportTimeoutEnvVarKey, value.ToString());
            var reader = CreatePeriodicExportingMetricReader();

            Assert.Equal(value, reader.ExportTimeoutMilliseconds);
        }

        [Fact]
        public void EnvironmentVariableNames()
        {
            Assert.Equal("OTEL_METRIC_EXPORT_INTERVAL", PeriodicExportingMetricReaderHelper.OTelMetricExportIntervalEnvVarKey);
            Assert.Equal("OTEL_METRIC_EXPORT_TIMEOUT", PeriodicExportingMetricReaderHelper.OTelMetricExportTimeoutEnvVarKey);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderHelper.OTelMetricExportIntervalEnvVarKey, null);
            Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderHelper.OTelMetricExportTimeoutEnvVarKey, null);
        }

        private static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
            MetricReaderOptions options = null)
        {
            if (options == null)
            {
                options = new();
            }

            var dummyMetricExporter = new InMemoryExporter<Metric>(new Metric[0]);
            return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(dummyMetricExporter, options);
        }
    }
}
