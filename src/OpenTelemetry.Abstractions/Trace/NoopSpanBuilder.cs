// <copyright file="NoopSpanBuilder.cs" company="OpenTelemetry Authors">
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
    using System.Diagnostics;

    /// <summary>
    /// No-op span builder.
    /// </summary>
    public class NoopSpanBuilder : ISpanBuilder
    {
        internal NoopSpanBuilder(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
        }

        /// <inheritdoc/>
        public ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            return this;
        }

        public ISpanBuilder SetStartTimestamp(DateTime startTimestamp)
        {
            return this;
        }

        /// <inheritdoc/>
        public ISpan StartSpan()
        {
            return BlankSpan.Instance;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetSampler(ISampler sampler)
        {
            if (sampler == null)
            {
                throw new ArgumentNullException(nameof(sampler));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(ISpan parentSpan)
        {
            if (parentSpan == null)
            {
                throw new ArgumentNullException(nameof(parentSpan));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(Activity parentActivity)
        {
            if (parentActivity == null)
            {
                throw new ArgumentNullException(nameof(parentActivity));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(SpanContext remoteParent)
        {
            if (remoteParent == null)
            {
                throw new ArgumentNullException(nameof(remoteParent));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetNoParent()
        {
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetCreateChild(bool createChild)
        {
            if (!createChild)
            {
                var currentActivity = Activity.Current;

                if (currentActivity == null)
                {
                    throw new ArgumentException("Current Activity cannot be null");
                }

                if (currentActivity.IdFormat != ActivityIdFormat.W3C)
                {
                    throw new ArgumentException("Current Activity is not in W3C format");
                }

                if (currentActivity.StartTimeUtc == default || currentActivity.Duration != default)
                {
                    throw new ArgumentException(
                        "Current Activity is not running: it has not been started or has been stopped");
                }
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetSpanKind(SpanKind spanKind)
        {
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(SpanContext spanContext)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(ILink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(SpanContext context, IDictionary<string, object> attributes)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            return this;
        }
    }
}
