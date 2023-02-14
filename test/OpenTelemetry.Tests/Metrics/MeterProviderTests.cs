// <copyright file="MeterProviderTests.cs" company="OpenTelemetry Authors">
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

using System.Reflection;
using System.Text.RegularExpressions;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MeterProviderTests
    {
        [Fact]
        public void MeterProviderFindExporterTest()
        {
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .Build();

            Assert.True(meterProvider.TryFindExporter(out InMemoryExporter<Metric> inMemoryExporter));
            Assert.False(meterProvider.TryFindExporter(out MyExporter myExporter));
        }

        [Fact]
        public void InstrumentNameRegexReflectionReplacementTest()
        {
            var meterProviderBuilderSdkType = typeof(MeterProviderBuilderSdk).Assembly.GetType("OpenTelemetry.Metrics.MeterProviderBuilderSdk", throwOnError: false)
                ?? throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk type could not be found reflectively.");

            var instrumentNameRegexProperty = meterProviderBuilderSdkType.GetProperty("InstrumentNameRegex", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk.InstrumentNameRegex field could not be found reflectively.");

            if (instrumentNameRegexProperty.GetValue(null) is not Regex originalInstrumentNameRegex)
            {
                throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk.InstrumentNameRegex return null when accessed reflectively.");
            }

            Assert.DoesNotMatch(originalInstrumentNameRegex, "Metric\\Name");

            try
            {
                instrumentNameRegexProperty.SetValue(null, new Regex(".*"));

                if (instrumentNameRegexProperty.GetValue(null) is not Regex instrumentNameRegex)
                {
                    throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk.InstrumentNameRegex return null when accessed reflectively.");
                }

                Assert.Matches(instrumentNameRegex, "Metric\\Name");
            }
            finally
            {
                instrumentNameRegexProperty.SetValue(null, originalInstrumentNameRegex);
            }
        }

        private class MyExporter : BaseExporter<Metric>
        {
            public override ExportResult Export(in Batch<Metric> batch)
            {
                return ExportResult.Success;
            }
        }
    }
}
