// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace System.Diagnostics.Metrics
{
    public class Counter<T> : MeterInstrument<T>
        where T : unmanaged
    {
        internal Counter(Meter meter, string name, string? description, string? unit) :
            base(meter, name, description, unit)
        {
            Publish();
        }

        public void Add(T measurement) => RecordMeasurement(measurement);
        public void Add(T measurement,
            (string LabelName, object LabelValue) label1) => RecordMeasurement(measurement, label1);
        public void Add(T measurement,
            (string LabelName, object LabelValue) label1,
            (string LabelName, object LabelValue) label2) => RecordMeasurement(measurement, label1, label2);
        public void Add(T measurement,
            (string LabelName, object LabelValue) label1,
            (string LabelName, object LabelValue) label2,
            (string LabelName, object LabelValue) label3) => RecordMeasurement(measurement, label1, label2, label3);
        public void Add(T measurement, params (string LabelName, object LabelValue)[] labels) => RecordMeasurement(measurement, labels);
    }
}
