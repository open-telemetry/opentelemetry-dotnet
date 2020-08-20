// <copyright file="BaggageContext.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Baggage context.
    /// </summary>
    public readonly struct BaggageContext : IEquatable<BaggageContext>
    {
        private static readonly RuntimeContextSlot<BaggageContext> RuntimeContextSlot = RuntimeContext.RegisterSlot<BaggageContext>("otel.baggage_context");
        private static readonly Dictionary<string, string> EmptyBaggage = new Dictionary<string, string>();

        private readonly Dictionary<string, string> baggage;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaggageContext"/> struct.
        /// </summary>
        /// <param name="baggage">Baggage key/value pairs.</param>
        public BaggageContext(Dictionary<string, string> baggage = null)
        {
            this.baggage = baggage ?? EmptyBaggage;
        }

        /// <summary>
        /// Gets or sets the current <see cref="BaggageContext"/>.
        /// </summary>
        public static BaggageContext Current
        {
            get => RuntimeContextSlot.Get();
            set => RuntimeContextSlot.Set(value);
        }

        /// <summary>
        /// Gets the number of key/value pairs in the baggage.
        /// </summary>
        public int Count => this.baggage.Count;

        /// <summary>
        /// Compare two entries of <see cref="BaggageContext"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator ==(BaggageContext left, BaggageContext right) => left.Equals(right);

        /// <summary>
        /// Compare two entries of <see cref="BaggageContext"/> for not equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator !=(BaggageContext left, BaggageContext right) => !(left == right);

        /// <summary>
        /// Returns the name/value pairs in the <see cref="BaggageContext"/>.
        /// </summary>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>Baggage key/value pairs.</returns>
        public static IReadOnlyDictionary<string, string> GetBaggage(BaggageContext baggageContext = default)
            => baggageContext == default ? Current.GetBaggage() : baggageContext.GetBaggage();

        /// <summary>
        /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
        public static object GetBaggage(string name, BaggageContext baggageContext = default)
            => baggageContext == default ? Current.GetBaggage(name) : baggageContext.GetBaggage(name);

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="value">Baggage item value.</param>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public static BaggageContext SetBaggage(string name, string value, BaggageContext baggageContext = default)
            => baggageContext == default ? Current.SetBaggage(name, value) : baggageContext.SetBaggage(name, value);

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <param name="baggage">Baggage key/value pairs.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public static BaggageContext SetBaggage(BaggageContext baggageContext = default, params KeyValuePair<string, string>[] baggage)
            => baggageContext == default ? Current.SetBaggage(baggage) : baggageContext.SetBaggage(baggage);

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggage">Baggage key/value pairs.</param>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public static BaggageContext SetBaggage(IEnumerable<KeyValuePair<string, string>> baggage, BaggageContext baggageContext = default)
           => baggageContext == default ? Current.SetBaggage(baggage) : baggageContext.SetBaggage(baggage);

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> with the key/value pair removed.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public static BaggageContext RemoveBaggage(string name, BaggageContext baggageContext = default)
            => baggageContext == default ? Current.RemoveBaggage(name) : baggageContext.RemoveBaggage(name);

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> with all the key/value pairs removed.
        /// </summary>
        /// <param name="baggageContext">Optional <see cref="BaggageContext"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public static BaggageContext ClearBaggage(BaggageContext baggageContext = default)
            => baggageContext == default ? Current.ClearBaggage() : baggageContext.ClearBaggage();

        /// <summary>
        /// Returns the name/value pairs in the <see cref="BaggageContext"/>.
        /// </summary>
        /// <returns>Baggage key/value pairs.</returns>
        public IReadOnlyDictionary<string, string> GetBaggage()
            => this.baggage;

        /// <summary>
        /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
        public object GetBaggage(string name)
        {
            return this.baggage.TryGetValue(name, out string value)
                ? value
                : null;
        }

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="value">Baggage item value.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public BaggageContext SetBaggage(string name, string value)
        {
            var baggageContext = new BaggageContext(
                new Dictionary<string, string>(this.baggage, StringComparer.OrdinalIgnoreCase)
                {
                    [name] = value,
                });

            Current = baggageContext;

            return baggageContext;
        }

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggage">Baggage key/value pairs.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public BaggageContext SetBaggage(params KeyValuePair<string, string>[] baggage)
            => this.SetBaggage((IEnumerable<KeyValuePair<string, string>>)baggage);

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggage">Baggage key/value pairs.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public BaggageContext SetBaggage(IEnumerable<KeyValuePair<string, string>> baggage)
        {
            var newBaggage = new Dictionary<string, string>(this.baggage, StringComparer.OrdinalIgnoreCase);

            foreach (var item in baggage)
            {
                newBaggage[item.Key] = item.Value;
            }

            var baggageContext = new BaggageContext(newBaggage);
            Current = baggageContext;
            return baggageContext;
        }

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> with the key/value pair removed.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public BaggageContext RemoveBaggage(string name)
        {
            var baggage = new Dictionary<string, string>(this.baggage, StringComparer.OrdinalIgnoreCase);
            baggage.Remove(name);
            var baggageContext = new BaggageContext(baggage);
            Current = baggageContext;
            return baggageContext;
        }

        /// <summary>
        /// Returns a new <see cref="BaggageContext"/> with all the key/value pairs removed.
        /// </summary>
        /// <returns>New <see cref="BaggageContext"/> containing the key/value pair.</returns>
        public BaggageContext ClearBaggage()
        {
            var baggageContext = new BaggageContext(null);
            Current = baggageContext;
            return baggageContext;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="BaggageContext"/>.
        /// </summary>
        /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
        public Dictionary<string, string>.Enumerator GetEnumerator()
            => this.baggage.GetEnumerator();

        /// <inheritdoc/>
        public bool Equals(BaggageContext other)
            => this.baggage.SequenceEqual(other.baggage);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => (obj is BaggageContext baggageContext) && this.Equals(baggageContext);

        /// <inheritdoc/>
        public override int GetHashCode()
            => this.baggage.GetHashCode();
    }
}
