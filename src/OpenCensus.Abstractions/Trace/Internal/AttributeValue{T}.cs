// <copyright file="AttributeValue{T}.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using System;

    /// <summary>
    /// Generic attribute value.
    /// </summary>
    /// <typeparam name="T">Type of the value carried by this attribute value.</typeparam>
    internal sealed class AttributeValue<T> : AttributeValue, IAttributeValue<T>
    {
        internal AttributeValue(T value)
        {
            this.Value = value;
        }

        /// <inheritdoc/>
        public T Value { get; }

        /// <summary>
        /// Creates an attribute value from string.
        /// </summary>
        /// <param name="stringValue">Value to populate attribute value with.</param>
        /// <returns>Attribute value with the given value.</returns>
        public static IAttributeValue<string> Create(string stringValue)
        {
            if (stringValue == null)
            {
                throw new ArgumentNullException(nameof(stringValue));
            }

            return new AttributeValue<string>(stringValue);
        }

        /// <summary>
        /// Creates an attribute value from long.
        /// </summary>
        /// <param name="longValue">Value to populate attribute value with.</param>
        /// <returns>Attribute value with the given value.</returns>
        public static IAttributeValue<long> Create(long longValue)
        {
            return new AttributeValue<long>(longValue);
        }

        /// <summary>
        /// Creates an attribute value from bool.
        /// </summary>
        /// <param name="booleanValue">Value to populate attribute value with.</param>
        /// <returns>Attribute value with the given value.</returns>
        public static IAttributeValue<bool> Create(bool booleanValue)
        {
            return new AttributeValue<bool>(booleanValue);
        }

        /// <summary>
        /// Creates an attribute value from double.
        /// </summary>
        /// <param name="doubleValue">Value to populate attribute value with.</param>
        /// <returns>Attribute value with the given value.</returns>
        public static IAttributeValue<double> Create(double doubleValue)
        {
            return new AttributeValue<double>(doubleValue);
        }

        /// <inheritdoc/>
        public TArg Apply<TArg>(Func<T, TArg> function)
        {
            return function(this.Value);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (obj is AttributeValue<T> attribute)
            {
                return attribute.Value.Equals(this.Value);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.Value.GetHashCode();
            return h;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "AttributeValue{"
                + "Value=" + this.Value.ToString()
                + "}";
        }

        /// <inheritdoc/>
        public override TReturn Match<TReturn>(
            Func<string, TReturn> stringFunction,
            Func<bool, TReturn> booleanFunction,
            Func<long, TReturn> longFunction,
            Func<double, TReturn> doubleFunction,
            Func<object, TReturn> defaultFunction)
        {
            if (typeof(T) == typeof(string))
            {
                string value = this.Value as string;
                return stringFunction(value);
            }
            else if (typeof(T) == typeof(long))
            {
                long val = (long)(object)this.Value;
                return longFunction(val);
            }
            else if (typeof(T) == typeof(bool))
            {
                bool val = (bool)(object)this.Value;
                return booleanFunction(val);
            }
            else if (typeof(T) == typeof(double))
            {
                double val = (double)(object)this.Value;
                return doubleFunction(val);
            }

            return defaultFunction(this.Value);
        }
    }
}
