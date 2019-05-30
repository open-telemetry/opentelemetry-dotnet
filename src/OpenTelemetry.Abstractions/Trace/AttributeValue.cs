// <copyright file="AttributeValue.cs" company="OpenTelemetry Authors">
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

    /// <summary>
    /// Attribute value.
    /// </summary>
    public abstract class AttributeValue
    {
        internal AttributeValue()
        {
        }

        public enum Type {
            STRING,
            BOOLEAN,
            LONG,
            DOUBLE
        }

        public new abstract Type GetType();

        /// <summary>
        /// Creates string attribute value from value provided.
        /// </summary>
        /// <param name="stringValue">String value.</param>
        /// <returns>Attribute value encapsulating the provided string value.</returns>
        public static IAttributeValue<string> StringAttributeValue(string stringValue)
        {
            if (stringValue == null)
            {
                throw new ArgumentNullException(nameof(stringValue));
            }

            return new AttributeValue<string>(stringValue);
        }

        /// <summary>
        /// Creates long attribute value from value provided.
        /// </summary>
        /// <param name="longValue">Long value.</param>
        /// <returns>Attribute value encapsulating the provided long value.</returns>
        public static IAttributeValue<long> LongAttributeValue(long longValue)
        {
            return new AttributeValue<long>(longValue);
        }

        /// <summary>
        /// Creates boolean attribute value from value provided.
        /// </summary>
        /// <param name="booleanValue">Boolean value.</param>
        /// <returns>Attribute value encapsulating the provided boolean value.</returns>
        public static IAttributeValue<bool> BooleanAttributeValue(bool booleanValue)
        {
            return new AttributeValue<bool>(booleanValue);
        }

        /// <summary>
        /// Creates double attribute value from value provided.
        /// </summary>
        /// <param name="doubleValue">Double value.</param>
        /// <returns>Attribute value encapsulating the provided double value.</returns>
        public static IAttributeValue<double> DoubleAttributeValue(double doubleValue)
        {
            return new AttributeValue<double>(doubleValue);
        }

        public String GetStringValue() {
            throw new InvalidOperationException($"This type can only return {GetType()} data");
        }

        public Boolean GetBooleanValue() {
            throw new InvalidOperationException($"This type can only return {GetType()} data");
        }

        public long GetLongValue() {
            throw new InvalidOperationException($"This type can only return {GetType()} data");
        }

        public Double GetDoubleValue() {
            throw new InvalidOperationException($"This type can only return {GetType()} data");
        }

        /// <inheritdoc/>
        public abstract T Match<T>(
            Func<string, T> stringFunction,
            Func<bool, T> booleanFunction,
            Func<long, T> longFunction,
            Func<double, T> doubleFunction,
            Func<object, T> defaultFunction);
    }

    abstract class AttributeValueBoolean : AttributeValue {
        AttributeValueBoolean() 
        {
        }

        static AttributeValue Create(Boolean booleanValue) {
            return new AttributeValue<bool>(booleanValue);
        }
        public override Type GetType() {
            return AttributeValue.Type.BOOLEAN;
        }

        public new abstract Boolean GetBooleanValue();
    }

    abstract class AttributeValueString : AttributeValue {
        AttributeValueString()
        {
        }

        static AttributeValue Create(String stringValue) 
        {
            return new AttributeValue<String>(stringValue);
        }

        public override Type GetType() 
        {
            return AttributeValue.Type.STRING;
        }
        public new abstract String GetStringValue();
    }

    abstract class AttributeValueLong : AttributeValue {
        AttributeValueLong()
        {
        }

        static AttributeValue Create(long longValue) 
        {
            return new AttributeValue<long>(longValue);
        }

        public override Type GetType()
        {
            return AttributeValue.Type.LONG;
        }

        public new abstract long GetLongValue();
    }

    abstract class AttributeValueDouble : AttributeValue {
        AttributeValueDouble()
        {
        }

        static AttributeValue Create(double doubleValue)
        {
            return new AttributeValue<double>(doubleValue);
        }

        public override Type GetType() 
        {
            return AttributeValue.Type.DOUBLE;
        }

        public new abstract Double GetDoubleValue();

    }

}
