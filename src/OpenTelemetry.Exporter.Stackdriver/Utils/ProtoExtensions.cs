// <copyright file="ProtoExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Stackdriver.Utils
{
    using OpenTelemetry.Common;

    /// <summary>
    /// Translation methods from OpenTelemetry structures to common
    /// Protobuf structures.
    /// </summary>
    public static class ProtoExtensions
    {
        /// <summary>
        /// Translates OpenTelemetry Timestamp to Protobuf's timestamp.
        /// </summary>
        /// <param name="timestamp">OpenTelemetry timestamp.</param>
        /// <returns>Protobuf's timestamp.</returns>
        public static Google.Protobuf.WellKnownTypes.Timestamp ToTimestamp(this Timestamp timestamp)
        {
            return new Google.Protobuf.WellKnownTypes.Timestamp { Seconds = timestamp.Seconds, Nanos = timestamp.Nanos };
        }
    }
}
