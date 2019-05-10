// <copyright file="TagValues.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tags
{
    using System.Collections.Generic;
    using OpenTelemetry.Abstractions.Utils;

    /// <summary>
    /// Collection of tags.
    /// </summary>
    public sealed class TagValues
    {
        private TagValues(IReadOnlyList<ITagValue> values)
        {
            this.Values = values;
        }

        /// <summary>
        /// Gets the collection of tag values.
        /// </summary>
        public IReadOnlyList<ITagValue> Values { get; }

        /// <summary>
        /// Create tag values out of list of tags.
        /// </summary>
        /// <param name="values">Values to create tag values from.</param>
        /// <returns>Resulting tag values collection.</returns>
        public static TagValues Create(IReadOnlyList<ITagValue> values)
        {
            return new TagValues(values);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TagValues{"
                + "values=" + Collections.ToString(this.Values)
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is TagValues that)
            {
                if (this.Values.Count != that.Values.Count)
                {
                    return false;
                }

                for (int i = 0; i < this.Values.Count; i++)
                {
                    if (this.Values[i] == null)
                    {
                        if (that.Values[i] != null)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!this.Values[i].Equals(that.Values[i]))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            foreach (var v in this.Values)
            {
                if (v != null)
                {
                    h ^= v.GetHashCode();
                }
            }

            return h;
        }
    }
}
