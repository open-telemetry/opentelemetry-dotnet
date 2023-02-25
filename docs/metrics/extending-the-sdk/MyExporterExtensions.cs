// <copyright file="MyExporterExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;

internal static class MyExporterExtensions
{
    public static MeterProviderBuilder AddMyExporter(this MeterProviderBuilder builder, int exportIntervalMilliSeconds = Timeout.Infinite)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (exportIntervalMilliSeconds == Timeout.Infinite)
        {
            // Export triggered manually only.
            return builder.AddReader(new BaseExportingMetricReader(new MyExporter()));
        }
        else
        {
            // Export is triggered periodically.
            return builder.AddReader(new PeriodicExportingMetricReader(new MyExporter(), exportIntervalMilliSeconds));
        }
    }
}
