// <copyright file="IAttributeValue.cs" company="OpenCensus Authors">
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
    /// Attribute value.
    /// </summary>
    public interface IAttributeValue
    {
        /// <summary>
        /// Executes type-specific callback without type casting.
        /// </summary>
        /// <typeparam name="T">Callback return value.</typeparam>
        /// <param name="stringFunction">Callback to call for string.</param>
        /// <param name="booleanFunction">Callback to call for boolean.</param>
        /// <param name="longFunction">Callback to call for long.</param>
        /// <param name="doubleFunction">Callback to call for double.</param>
        /// <param name="defaultFunction">Callback to call for any other type.</param>
        /// <returns>Callback execution result.</returns>
        T Match<T>(
             Func<string, T> stringFunction,
             Func<bool, T> booleanFunction,
             Func<long, T> longFunction,
             Func<double, T> doubleFunction,
             Func<object, T> defaultFunction);
    }

    /// <summary>
    /// Generic attribute value interface.
    /// </summary>
    /// <typeparam name="TAttr">Type of the attribute.</typeparam>
    public interface IAttributeValue<TAttr> : IAttributeValue
    {
        /// <summary>
        /// Gets the attribute value.
        /// </summary>
        TAttr Value { get; }

        /// <summary>
        /// Executes type specific callback.
        /// </summary>
        /// <typeparam name="T">Callback result type.</typeparam>
        /// <param name="function">Callback to execute.</param>
        /// <returns>Result of callback execution.</returns>
        T Apply<T>(Func<TAttr, T> function);
    }
}
