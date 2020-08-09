// <copyright file="ITextFormat.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTelemetry.Context.Propagation
{
    public readonly struct TextFormatContext : IEquatable<TextFormatContext>
    {
        public ActivityContext ActivityContext { get; }

        public IEnumerable<KeyValuePair<string, string>> ActivityBaggage { get; }

        public TextFormatContext(ActivityContext activityContext, IEnumerable<KeyValuePair<string, string>> activityBaggage)
        {
            this.ActivityContext = activityContext;
            this.ActivityBaggage = activityBaggage;
        }

        public static bool operator ==(TextFormatContext left, TextFormatContext right) => left.Equals(right);

        public static bool operator !=(TextFormatContext left, TextFormatContext right) => !(left == right);

        public bool Equals(TextFormatContext value)
        {
            if (this.ActivityContext != value.ActivityContext
                || this.ActivityBaggage is null != value.ActivityBaggage is null)
            {
                return false;
            }

            if (this.ActivityBaggage is null)
            {
                return true;
            }

            if (this.ActivityBaggage.Count() != value.ActivityBaggage.Count())
            {
                return false;
            }

            var thisEnumerator = this.ActivityBaggage.GetEnumerator();
            var valueEnumerator = value.ActivityBaggage.GetEnumerator();

            while (thisEnumerator.MoveNext() && valueEnumerator.MoveNext())
            {
                if (thisEnumerator.Current.Key != valueEnumerator.Current.Key
                    || thisEnumerator.Current.Value != valueEnumerator.Current.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => (obj is TextFormatContext context) ? Equals(context) : false;
    }

    /// <summary>
    /// Text format wire context propagator. Helps to extract and inject context from textual
    /// representation (typically http headers or metadata collection).
    /// </summary>
    public interface ITextFormat
    {
        /// <summary>
        /// Gets the list of headers used by propagator. The use cases of this are:
        ///   * allow pre-allocation of fields, especially in systems like gRPC Metadata
        ///   * allow a single-pass over an iterator (ex OpenTracing has no getter in TextMap).
        /// </summary>
        ISet<string> Fields { get; }

        /// <summary>
        /// Injects textual representation of activity context to transmit over the wire.
        /// </summary>
        /// <typeparam name="T">Type of an object to set context on. Typically HttpRequest or similar.</typeparam>
        /// <param name="activity">Activity to transmit over the wire.</param>
        /// <param name="carrier">Object to set context on. Instance of this object will be passed to setter.</param>
        /// <param name="setter">Action that will set name and value pair on the object.</param>
        void Inject<T>(Activity activity, T carrier, Action<T, string, string> setter);

        /// <summary>
        /// Extracts activity context from textual representation.
        /// </summary>
        /// <typeparam name="T">Type of object to extract context from. Typically HttpRequest or similar.</typeparam>
        /// <param name="context">The default context to be used if Extract fails.</param>
        /// <param name="carrier">Object to extract context from. Instance of this object will be passed to the getter.</param>
        /// <param name="getter">Function that will return string value of a key with the specified name.</param>
        /// <returns>Context from it's text representation.</returns>
        TextFormatContext Extract<T>(TextFormatContext context, T carrier, Func<T, string, IEnumerable<string>> getter);
    }
}
