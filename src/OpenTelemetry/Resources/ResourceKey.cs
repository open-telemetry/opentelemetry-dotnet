// <copyright file="ResourceKey.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Globalization;
using OpenTelemetry.Utils;

namespace OpenTelemetry.Resources
{
    public readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        private readonly int intValue;
        private readonly double doubleValue;
        private readonly string stringValue;
        private readonly Type type;

        private ResourceKey(Type type, int intValue = 0, double doubleValue = 0, string stringValue = null)
        {
            this.type = type;
            this.intValue = intValue;
            this.doubleValue = doubleValue;
            this.stringValue = stringValue;
        }

        public static implicit operator ResourceKey(int value) => new ResourceKey(typeof(int), value);

        public static implicit operator ResourceKey(double value) =>
            new ResourceKey(typeof(double), doubleValue: value);

        public static implicit operator ResourceKey(string value) =>
            new ResourceKey(typeof(string), stringValue: value);

        public static bool operator ==(ResourceKey left, ResourceKey right) => left.Equals(right);

        public static bool operator !=(ResourceKey left, ResourceKey right) => !left.Equals(right);

        public bool Equals(ResourceKey other)
            => this.intValue == other.intValue
               && this.doubleValue == other.doubleValue
               && this.stringValue == other.stringValue
               && Equals(this.type, other.type);

        public override bool Equals(object obj) => obj is ResourceKey other && this.Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = this.intValue;
                hashCode = (hashCode * 397) ^ this.doubleValue.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.stringValue != null ? this.stringValue.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.type != null ? this.type.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            if (this.type == typeof(int))
            {
                return this.intValue.ToString(CultureInfo.InvariantCulture);
            }

            if (this.type == typeof(double))
            {
                return this.doubleValue.ToString(CultureInfo.InvariantCulture);
            }

            if (this.type == typeof(string))
            {
                return this.stringValue;
            }

            Debug.Fail("private ctor, only supported types are defined as implicit operators.");
            return null;
        }

        internal bool IsValid()
            => this != default
               // TODO: Any validation for int, double, bool?
               // && this.type != typeof(int) || this.intValue == 0
               // && this.type != typeof(double) || this.intValue == 0
               && (this.type != typeof(string)
                   || !string.IsNullOrWhiteSpace(this.stringValue)
                   || this.stringValue.Length > Resource.MaxResourceTypeNameLength
                   || !StringUtil.IsPrintableString(this.stringValue));
    }
}
