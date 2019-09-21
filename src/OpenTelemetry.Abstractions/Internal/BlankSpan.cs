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

namespace OpenTelemetry.Trace
{
    using System;
    using System.Collections.Generic;

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
        public SpanContext Context => SpanContext.Blank;

        /// <inheritdoc />
        public bool IsRecordingEvents => false;

        /// <inheritdoc />
        public Status Status
        {
            get => Status.Ok;

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException(nameof(value));
                }
            }
        }

        /// <inheritdoc />
        public bool HasEnded { get; }

        /// <inheritdoc />
        public void UpdateName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
        }

        /// <inheritdoc />
        public void SetAttribute(KeyValuePair<string, object> keyValuePair)
        {
            if (keyValuePair.Key == null || keyValuePair.Value == null)
            {
                throw new ArgumentNullException(nameof(keyValuePair));
            }
        }

        /// <inheritdoc />
        public void SetAttribute(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
        }

        /// <inheritdoc />
        public void SetAttribute(string key, long value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        /// <inheritdoc />
        public void SetAttribute(string key, double value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        /// <inheritdoc />
        public void SetAttribute(string key, bool value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        /// <inheritdoc />
        public void AddEvent(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
        }

        /// <inheritdoc />
        public void AddEvent(string name, IDictionary<string, object> attributes)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }
        }

        /// <inheritdoc />
        public void AddEvent(IEvent newEvent)
        {
            if (newEvent == null)
            {
                throw new ArgumentNullException(nameof(newEvent));
            }
        }

        /// <inheritdoc />
        public void AddLink(ILink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }
        }

        /// <inheritdoc />
        public void End()
        {
        }

        public void End(DateTime endTimestamp)
        {
        }
    }
}
