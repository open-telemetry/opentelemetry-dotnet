// <copyright file="PrometheusMetricBuilderTests.cs" company="OpenTelemetry Authors">
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

using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using OpenTelemetry.Exporter.Prometheus.Implementation;

using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests.Implementation
{
    public class PrometheusMetricBuilderTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("pt-BR")]
        public void Test(string cultureInfo)
        {
            if (!string.IsNullOrEmpty(cultureInfo))
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureInfo);
            }

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            var builder = new PrometheusMetricBuilder();
            builder.WithName("test-builder");
            builder.WithDescription("test-description");
            builder.WithType("test-type");

            var metricValueBuilder = builder.AddValue();
            metricValueBuilder = metricValueBuilder.WithValue(10.0123);
            metricValueBuilder.WithLabel("test-double", "double");
            builder.Write(writer);

            writer.Flush();

            // assert
            string actual = Encoding.UTF8.GetString(stream.ToArray());
            string[] lines = actual.Split('\n');
            Assert.Equal("# HELP test_buildertest-description", lines[0]);
            Assert.Equal("# TYPE test_builder test-type", lines[1]);
            Assert.StartsWith("test_builder{test_double=\"double\"} 10.01", lines[2]);
        }
    }
}
