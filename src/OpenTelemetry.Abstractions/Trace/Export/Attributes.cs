﻿// <copyright file="Attributes.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public sealed class Attributes : IAttributes
    {
        private static readonly Attributes Empty = new Attributes(new Dictionary<string, object>(), 0);

        internal Attributes(IDictionary<string, object> attributeMap, int droppedAttributesCount)
        {
            this.AttributeMap = attributeMap ?? throw new ArgumentNullException("Null attributeMap");
            this.DroppedAttributesCount = droppedAttributesCount;
        }

        public IDictionary<string, object> AttributeMap { get; }

        public int DroppedAttributesCount { get; }

        public static Attributes Create(IDictionary<string, object> attributeMap, int droppedAttributesCount)
        {
            if (attributeMap == null)
            {
                return Empty;
            }

            IDictionary<string, object> copy = new Dictionary<string, object>(attributeMap);
            return new Attributes(new ReadOnlyDictionary<string, object>(copy), droppedAttributesCount);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Attributes{"
                + "attributeMap=" + this.AttributeMap + ", "
                + "droppedAttributesCount=" + this.DroppedAttributesCount
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Attributes that)
            {
                return this.AttributeMap.SequenceEqual(that.AttributeMap)
                     && (this.DroppedAttributesCount == that.DroppedAttributesCount);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.AttributeMap.GetHashCode();
            h *= 1000003;
            h ^= this.DroppedAttributesCount;
            return h;
        }
    }
}
