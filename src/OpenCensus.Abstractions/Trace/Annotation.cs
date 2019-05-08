// <copyright file="Annotation.cs" company="OpenCensus Authors">
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
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using OpenCensus.Abstractions.Utils;

    public sealed class Annotation : IAnnotation
    {
        private static readonly ReadOnlyDictionary<string, IAttributeValue> EmptyAttributes =
                new ReadOnlyDictionary<string, IAttributeValue>(new Dictionary<string, IAttributeValue>());

        internal Annotation(string description, IDictionary<string, IAttributeValue> attributes)
        {
            this.Description = description ?? throw new ArgumentNullException("Null description");
            this.Attributes = attributes ?? throw new ArgumentNullException("Null attributes");
        }

        public string Description { get; }

        public IDictionary<string, IAttributeValue> Attributes { get; }

        public static IAnnotation FromDescription(string description)
        {
            return new Annotation(description, EmptyAttributes);
        }

        public static IAnnotation FromDescriptionAndAttributes(string description, IDictionary<string, IAttributeValue> attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            IDictionary<string, IAttributeValue> readOnly = new ReadOnlyDictionary<string, IAttributeValue>(attributes);
            return new Annotation(description, readOnly);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (obj is Annotation annotation)
            {
                return this.Description.Equals(annotation.Description) &&
                    this.Attributes.SequenceEqual(annotation.Attributes);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.Description.GetHashCode();
            h *= 1000003;
            h ^= this.Attributes.GetHashCode();
            return h;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Annotation{"
                + "description=" + this.Description + ", "
                + "attributes=" + Collections.ToString(this.Attributes)
                + "}";
        }
    }
}
