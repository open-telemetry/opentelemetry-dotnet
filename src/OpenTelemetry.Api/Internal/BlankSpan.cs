// <copyright file="BlankSpan.cs" company="OpenTelemetry Authors">
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
using System;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Blank span.
    /// </summary>
    public sealed class BlankSpan : ISpan
    {
        /// <summary>
        /// Blank span instance.
        /// </summary>
        public static readonly BlankSpan Instance = new BlankSpan();

        private BlankSpan()
        {
        }

        /// <inheritdoc />
        public SpanContext Context => SpanContext.BlankLocal;

        /// <inheritdoc />
        public bool IsRecording => false;

        /// <inheritdoc />
        public Status Status { get; set; } = Status.Ok;

        /// <inheritdoc />
        public void UpdateName(string name)
        {
        }

        /// <inheritdoc />
        public void SetAttribute(string key, object value)
        {
        }

        /// <inheritdoc />
        public void SetAttribute(string key, bool value)
        {
        }

        /// <inheritdoc />
        public void SetAttribute(string key, long value)
        {
        }

        /// <inheritdoc />
        public void SetAttribute(string key, double value)
        {
        }

        /// <inheritdoc />
        public void AddEvent(string name)
        {
        }

        /// <inheritdoc />
        public void AddEvent(Event newEvent)
        {
        }

        /// <inheritdoc />
        public void End()
        {
        }

        /// <inheritdoc />
        public void End(DateTimeOffset endTimestamp)
        {
        }
    }
}
