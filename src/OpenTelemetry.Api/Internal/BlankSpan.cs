// <copyright file="BlankSpan.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Blank span.
    /// </summary>
    internal sealed class BlankSpan : TelemetrySpan
    {
        /// <summary>
        /// Blank span instance.
        /// </summary>
        public static readonly BlankSpan Instance = new BlankSpan();

        private BlankSpan()
        {
        }

        /// <inheritdoc />
        public override SpanContext Context => default;

        /// <inheritdoc />
        public override bool IsRecording => false;

        /// <inheritdoc />
        public override Status Status
        {
            set { }
        }

        /// <inheritdoc />
        public override void UpdateName(string name)
        {
        }

        /// <inheritdoc />
        public override void SetAttribute(string key, object value)
        {
        }

        /// <inheritdoc />
        public override void SetAttribute(string key, bool value)
        {
        }

        /// <inheritdoc />
        public override void SetAttribute(string key, long value)
        {
        }

        /// <inheritdoc />
        public override void SetAttribute(string key, double value)
        {
        }

        /// <inheritdoc />
        public override void AddEvent(string name)
        {
        }

        /// <inheritdoc />
        public override void AddEvent(Event newEvent)
        {
        }

        /// <inheritdoc />
        public override void End()
        {
        }

        /// <inheritdoc />
        public override void End(DateTimeOffset endTimestamp)
        {
        }
    }
}
