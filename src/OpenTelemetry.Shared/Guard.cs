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

namespace OpenTelemetry.Shared
{
    public static class Guard
    {
        private const string DefaultParamName = "N/A";

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNull(object value, string paramName = DefaultParamName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName, "Must not be null");
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrEmpty(string value, string paramName = DefaultParamName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Must not be null or empty", paramName);
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrWhitespace(string value, string paramName = DefaultParamName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Must not be null or whitespace", paramName);
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotZero(int value, string message, string paramName = DefaultParamName)
        {
            if (value == 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotValidTimeout(int value, string paramName = DefaultParamName)
        {
            NotInRange(value, paramName, min: Timeout.Infinite, message: $"Must be non-negative or '{nameof(Timeout)}.{nameof(Timeout.Infinite)}'");
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotInRange(int value, string paramName = DefaultParamName, int min = int.MinValue, int max = int.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            NotInRange<int>(value, paramName, min, max, minName, maxName, message);
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotInRange(double value, string paramName = DefaultParamName, double min = double.MinValue, double max = double.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            NotInRange<double>(value, paramName, min, max, minName, maxName, message);
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NotOfType<T>(object value, string paramName = DefaultParamName)
        {
            if (value is not T result)
            {
                throw new InvalidCastException($"Cannot cast '{paramName}' from '{value.GetType().Name}' to '{typeof(T).Name}'");
            }

            return result;
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NotInRange<T>(T value, string paramName, T min, T max, string minName, string maxName, string message)
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
