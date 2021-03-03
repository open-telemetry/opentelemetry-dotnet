// <copyright file="Baggage.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context;

namespace OpenTelemetry
{
    /// <summary>
    /// Baggage implementation.
    /// </summary>
    /// <remarks>
    /// Spec reference: <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/baggage/api.md">Baggage API</a>.
    /// </remarks>
    public readonly struct Baggage : IEquatable<Baggage>
    {
        private static readonly RuntimeContextSlot<Baggage> RuntimeContextSlot = RuntimeContext.RegisterSlot<Baggage>("otel.baggage");
        private static readonly Dictionary<string, string> EmptyBaggage = new Dictionary<string, string>();

        private readonly Dictionary<string, string> baggage;

        /// <summary>
        /// Initializes a new instance of the <see cref="Baggage"/> struct.
        /// </summary>
        /// <param name="baggage">Baggage key/value pairs.</param>
        internal Baggage(Dictionary<string, string> baggage)
        {
            this.baggage = baggage;
        }

        /// <summary>
        /// Gets or sets the current <see cref="Baggage"/>.
        /// </summary>
        public static Baggage Current
        {
            get => RuntimeContextSlot.Get();
            set => RuntimeContextSlot.Set(value);
        }

        /// <summary>
        /// Gets the number of key/value pairs in the baggage.
        /// </summary>
        public int Count => this.baggage?.Count ?? 0;

        /// <summary>
        /// Compare two entries of <see cref="Baggage"/> for equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator ==(Baggage left, Baggage right) => left.Equals(right);

        /// <summary>
        /// Compare two entries of <see cref="Baggage"/> for not equality.
        /// </summary>
        /// <param name="left">First Entry to compare.</param>
        /// <param name="right">Second Entry to compare.</param>
        public static bool operator !=(Baggage left, Baggage right) => !(left == right);

        /// <summary>
        /// Create a <see cref="Baggage"/> instance from dictionary of baggage key/value pairs.
        /// </summary>
        /// <param name="baggageItems">Baggage key/value pairs.</param>
        /// <returns><see cref="Baggage"/>.</returns>
        public static Baggage Create(Dictionary<string, string> baggageItems = null)
        {
            if (baggageItems == null)
            {
                return default;
            }

            Dictionary<string, string> baggageCopy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> baggageItem in baggageItems)
            {
                if (string.IsNullOrEmpty(baggageItem.Value))
                {
                    baggageCopy.Remove(baggageItem.Key);
                    continue;
                }

                baggageCopy[baggageItem.Key] = baggageItem.Value;
            }

            return new Baggage(baggageCopy);
        }

        /// <summary>
        /// Returns the name/value pairs in the <see cref="Baggage"/>.
        /// </summary>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>Baggage key/value pairs.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public static IReadOnlyDictionary<string, string> GetBaggage(Baggage baggage = default)
            => baggage == default ? Current.GetBaggage() : baggage.GetBaggage();

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Baggage"/>.
        /// </summary>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
        public static Dictionary<string, string>.Enumerator GetEnumerator(Baggage baggage = default)
            => baggage == default ? Current.GetEnumerator() : baggage.GetEnumerator();

        /// <summary>
        /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public static string GetBaggage(string name, Baggage baggage = default)
            => baggage == default ? Current.GetBaggage(name) : baggage.GetBaggage(name);

        /// <summary>
        /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="value">Baggage item value.</param>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public static Baggage SetBaggage(string name, string value, Baggage baggage = default)
            => baggage == default ? Current.SetBaggage(name, value) : baggage.SetBaggage(name, value);

        /// <summary>
        /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggageItems">Baggage key/value pairs.</param>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "This was agreed on to be the friendliest API surface")]
        public static Baggage SetBaggage(IEnumerable<KeyValuePair<string, string>> baggageItems, Baggage baggage = default)
           => baggage == default ? Current.SetBaggage(baggageItems) : baggage.SetBaggage(baggageItems);

        /// <summary>
        /// Returns a new <see cref="Baggage"/> with the key/value pair removed.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public static Baggage RemoveBaggage(string name, Baggage baggage = default)
            => baggage == default ? Current.RemoveBaggage(name) : baggage.RemoveBaggage(name);

        /// <summary>
        /// Returns a new <see cref="Baggage"/> with all the key/value pairs removed.
        /// </summary>
        /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public static Baggage ClearBaggage(Baggage baggage = default)
            => baggage == default ? Current.ClearBaggage() : baggage.ClearBaggage();

        /// <summary>
        /// Returns the name/value pairs in the <see cref="Baggage"/>.
        /// </summary>
        /// <returns>Baggage key/value pairs.</returns>
        public IReadOnlyDictionary<string, string> GetBaggage()
            => this.baggage ?? EmptyBaggage;

        /// <summary>
        /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
        public string GetBaggage(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            return this.baggage != null && this.baggage.TryGetValue(name, out string value)
                ? value
                : null;
        }

        /// <summary>
        /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <param name="value">Baggage item value.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public Baggage SetBaggage(string name, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return this.RemoveBaggage(name);
            }

            return Current = new Baggage(
                new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase)
                {
                    [name] = value,
                });
        }

        /// <summary>
        /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggageItems">Baggage key/value pairs.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public Baggage SetBaggage(params KeyValuePair<string, string>[] baggageItems)
            => this.SetBaggage((IEnumerable<KeyValuePair<string, string>>)baggageItems);

        /// <summary>
        /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
        /// </summary>
        /// <param name="baggageItems">Baggage key/value pairs.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public Baggage SetBaggage(IEnumerable<KeyValuePair<string, string>> baggageItems)
        {
            if ((baggageItems?.Count() ?? 0) <= 0)
            {
                return this;
            }

            var newBaggage = new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase);

            foreach (var item in baggageItems)
            {
                if (string.IsNullOrEmpty(item.Value))
                {
                    newBaggage.Remove(item.Key);
                }
                else
                {
                    newBaggage[item.Key] = item.Value;
                }
            }

            return Current = new Baggage(newBaggage);
        }

        /// <summary>
        /// Returns a new <see cref="Baggage"/> with the key/value pair removed.
        /// </summary>
        /// <param name="name">Baggage item name.</param>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public Baggage RemoveBaggage(string name)
        {
            var baggage = new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase);
            baggage.Remove(name);

            return Current = new Baggage(baggage);
        }

        /// <summary>
        /// Returns a new <see cref="Baggage"/> with all the key/value pairs removed.
        /// </summary>
        /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
        public Baggage ClearBaggage()
            => Current = default;

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Baggage"/>.
        /// </summary>
        /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
        public Dictionary<string, string>.Enumerator GetEnumerator()
            => (this.baggage ?? EmptyBaggage).GetEnumerator();

        /// <inheritdoc/>
        public bool Equals(Baggage other)
        {
            bool baggageIsNullOrEmpty = this.baggage == null || this.baggage.Count <= 0;

            if (baggageIsNullOrEmpty != (other.baggage == null || other.baggage.Count <= 0))
            {
                return false;
            }

            return baggageIsNullOrEmpty || this.baggage.SequenceEqual(other.baggage);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => (obj is Baggage baggage) && this.Equals(baggage);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var baggage = this.baggage ?? EmptyBaggage;

            unchecked
            {
                int res = 17;
                foreach (var item in baggage)
                {
                    res = (res * 23) + baggage.Comparer.GetHashCode(item.Key);
                    res = (res * 23) + item.Value.GetHashCode();
                }

                return res;
            }
        }
    }
}
