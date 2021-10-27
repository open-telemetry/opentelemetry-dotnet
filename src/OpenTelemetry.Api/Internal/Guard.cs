// <copyright file="Guard.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// Methods for guarding against exception throwing values.
    /// </summary>
    internal static class Guard
    {
        private const string DefaultParamName = "N/A";

        /// <summary>
        /// Throw an exception if the value is null.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Null(object value, string paramName = DefaultParamName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName, "Must not be null");
            }
        }

        /// <summary>
        /// Throw an exception if the value is null or empty.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NullOrEmpty(string value, string paramName = DefaultParamName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Must not be null or empty", paramName);
            }
        }

        /// <summary>
        /// Throw an exception if the value is null or whitespace.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NullOrWhitespace(string value, string paramName = DefaultParamName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Must not be null or whitespace", paramName);
            }
        }

        /// <summary>
        /// Throw an exception if the value is zero.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="message">The message to use in the thrown exception.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Zero(int value, string message = "Must not be zero", string paramName = DefaultParamName)
        {
            if (value == 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>
        /// Throw an exception if the value is not considered a valid timeout.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvalidTimeout(int value, string paramName = DefaultParamName)
        {
            Range(value, paramName, min: Timeout.Infinite, message: $"Must be non-negative or '{nameof(Timeout)}.{nameof(Timeout.Infinite)}'");
        }

        /// <summary>
        /// Throw an exception if the value is not within the given range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <param name="minName">The name of the lower bound.</param>
        /// <param name="maxName">The name of the upper bound.</param>
        /// <param name="message">An optional custom message to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Range(int value, string paramName = DefaultParamName, int min = int.MinValue, int max = int.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            Range<int>(value, paramName, min, max, minName, maxName, message);
        }

        /// <summary>
        /// Throw an exception if the value is not within the given range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <param name="minName">The name of the lower bound.</param>
        /// <param name="maxName">The name of the upper bound.</param>
        /// <param name="message">An optional custom message to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Range(double value, string paramName = DefaultParamName, double min = double.MinValue, double max = double.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            Range<double>(value, paramName, min, max, minName, maxName, message);
        }

        /// <summary>
        /// Throw an exception if the value is not of the expected type.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        /// <typeparam name="T">The type attempted to convert to.</typeparam>
        /// <returns>The value casted to the specified type.</returns>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Type<T>(object value, string paramName = DefaultParamName)
        {
            if (value is not T result)
            {
                throw new InvalidCastException($"Cannot cast '{paramName}' from '{value.GetType().Name}' to '{typeof(T).Name}'");
            }

            return result;
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Range<T>(T value, string paramName, T min, T max, string minName, string maxName, string message)
            where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                var minMessage = minName != null ? $": {minName}" : string.Empty;
                var maxMessage = maxName != null ? $": {maxName}" : string.Empty;
                var exMessage = message ?? $"Must be in the range: [{min}{minMessage}, {max}{maxMessage}]";
                throw new ArgumentOutOfRangeException(paramName, value, exMessage);
            }
        }
    }
}
