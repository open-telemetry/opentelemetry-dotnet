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
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNull(object value, string paramName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName, $"'{paramName}' is null");
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(paramName, $"'{paramName}' is null or empty");
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrWhitespace(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(paramName, $"'{paramName}' is null or whitespace");
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotZero(int value, string paramName, string message)
        {
            if (value == 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotValidTimeout(int value, string paramName)
        {
            NotInRange(value, paramName, min: Timeout.Infinite, message: $"'{paramName}' = '{value}' must be non-negative or '{nameof(Timeout)}.{nameof(Timeout.Infinite)}'");
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotInRange(int value, string paramName, int min = int.MinValue, int max = int.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            if (value < min || value > max)
            {
                var exMessage = message ?? InRangeString(paramName, min, max, minName, maxName);
                throw new ArgumentOutOfRangeException(paramName, value, exMessage);
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotInRange(double value, string paramName, double min = double.MinValue, double max = double.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            if (value < min || value > max)
            {
                var exMessage = message ?? InRangeString(paramName, min, max, minName, maxName);
                throw new ArgumentOutOfRangeException(paramName, value, exMessage);
            }
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NotOfType<T>(object value, string paramName)
        {
            if (value is not T result)
            {
                throw new InvalidCastException($"Cannot cast '{paramName}' to type '{typeof(T).Name}'");
            }

            return result;
        }

        private static string InRangeString(string paramName, double min = double.MinValue, double max = double.MaxValue, string minName = null, string maxName = null)
        {
            var minMessage = minName != null ? $": {minName}" : string.Empty;
            var maxMessage = maxName != null ? $": {maxName}" : string.Empty;
            return $"'{paramName}' must be in the range: [{min}{minMessage}, {max}{maxMessage}]";
        }
    }
}
