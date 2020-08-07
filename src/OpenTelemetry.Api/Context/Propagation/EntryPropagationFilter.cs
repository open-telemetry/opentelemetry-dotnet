// <copyright file="EntryPropagationFilter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// Filter defining propagation rules for <see cref="CorrelationContextEntry"/>.
    /// </summary>
    public readonly struct EntryPropagationFilter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntryPropagationFilter"/> struct.
        /// </summary>
        /// <param name="op">Operator to apply.</param>
        /// <param name="matchString">String to apply the operator with.</param>
        /// <param name="action">Action to execute on entry.</param>
        public EntryPropagationFilter(FilterMatchOperator op, string matchString, Action<CorrelationContextEntry> action)
        {
            this.Operator = op;
            this.MatchString = matchString;
            this.Action = action;
        }

        /// <summary>
        /// Operator to use in a filter.
        /// </summary>
        public enum FilterMatchOperator
        {
            /// <summary>
            /// Equals operator.
            /// </summary>
            Equal,

            /// <summary>
            /// Not equals operator.
            /// </summary>
            NotEqual,

            /// <summary>
            /// Operator checking the prefix.
            /// </summary>
            HasPrefix,
        }

        internal FilterMatchOperator Operator { get; }

        internal string MatchString { get; }

        internal Action<CorrelationContextEntry> Action { get; }

        /// <summary>
        /// Check whether <see cref="CorrelationContextEntry"/> matches this filter pattern.
        /// </summary>
        /// <param name="entry">Distributed Context entry to check.</param>
        /// <returns>True if <see cref="CorrelationContextEntry"/> matches this filter, false - otherwise.</returns>
        public bool IsMatch(CorrelationContextEntry entry)
        {
            bool result = false;
            switch (this.Operator)
            {
                case FilterMatchOperator.Equal: result = entry.Key.Equals(this.MatchString); break;
                case FilterMatchOperator.NotEqual: result = !entry.Key.Equals(this.MatchString); break;
                case FilterMatchOperator.HasPrefix: result = entry.Key.StartsWith(this.MatchString, StringComparison.Ordinal); break;
            }

            if (result)
            {
                this.Action(entry);
            }

            return result;
        }
    }
}
