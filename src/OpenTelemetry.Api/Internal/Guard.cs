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

#if !NETCOREAPP3_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Allows capturing of the expressions passed to a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
    internal sealed class CallerArgumentExpressionAttribute : Attribute
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CallerArgumentExpressionAttribute"/> class.
        /// </summary>
        /// <param name="parameterName">The name of the targeted parameter.</param>
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            this.ParameterName = parameterName;
        }

        /// <summary>
        /// Gets the target parameter name of the CallerArgumentExpression.
        /// </summary>
        public string ParameterName { get; }
    }
}
#endif

#pragma warning disable SA1403 // File may only contain a single namespace
namespace OpenTelemetry.Internal
#pragma warning restore SA1403 // File may only contain a single namespace
{
    /// <summary>
    /// Methods for guarding against exception throwing values.
    /// </summary>
    internal static class Guard
    {
        /// <summary>
        /// Throw an exception if the value is null.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="paramName">The parameter name to use in the thrown exception.</param>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull(object value, [CallerArgumentExpression("value")] string paramName = null)
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
        public static void ThrowIfNullOrEmpty(string value, [CallerArgumentExpression("value")] string paramName = null)
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
        public static void ThrowIfNullOrWhitespace(string value, [CallerArgumentExpression("value")] string paramName = null)
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
        public static void ThrowIfZero(int value, string message = "Must not be zero", [CallerArgumentExpression("value")] string paramName = null)
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
        public static void ThrowIfInvalidTimeout(int value, [CallerArgumentExpression("value")] string paramName = null)
        {
            ThrowIfOutOfRange(value, paramName, min: Timeout.Infinite, message: $"Must be non-negative or '{nameof(Timeout)}.{nameof(Timeout.Infinite)}'");
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
        public static void ThrowIfOutOfRange(int value, [CallerArgumentExpression("value")] string paramName = null, int min = int.MinValue, int max = int.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            Range(value, paramName, min, max, minName, maxName, message);
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
        public static void ThrowIfOutOfRange(double value, [CallerArgumentExpression("value")] string paramName = null, double min = double.MinValue, double max = double.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            Range(value, paramName, min, max, minName, maxName, message);
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
        public static T ThrowIfNotOfType<T>(object value, [CallerArgumentExpression("value")] string paramName = null)
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
