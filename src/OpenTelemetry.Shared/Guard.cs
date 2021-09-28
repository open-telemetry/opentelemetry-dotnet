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
using System.Threading;

namespace OpenTelemetry.Shared
{
    public static class Guard
    {
        [DebuggerHidden]
        public static void IsNotNull(object value, string paramName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        [DebuggerHidden]
        public static void IsNotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(paramName, "Value is null or empty");
            }
        }

        [DebuggerHidden]
        public static void IsNotNullOrWhitespace(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(paramName, "Value is null or whitespace");
            }
        }

        [DebuggerHidden]
        public static void IsNotZero(int value, string paramName, string message)
        {
            IsNotEqual(value, 0, paramName, message);
        }

        [DebuggerHidden]
        public static void IsNotEqual(int value, int compare, string paramName, string message)
        {
            if (value == compare)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        [DebuggerHidden]
        public static void IsNotValidTimeout(int value, string paramName)
        {
            IsNotInRange(value, paramName, min: Timeout.Infinite, message: $"Must be non-negative or {nameof(Timeout.Infinite)}");
        }

        [DebuggerHidden]
        public static void IsNotInRange(int value, string paramName, int min = int.MinValue, int max = int.MaxValue, string minName = null, string maxName = null, string message = null)
        {
            Debug.Assert(min != max, $"Please supply a non-default value for either '{nameof(min)}' or '{nameof(max)}'");

            var invalid = false;
            var exMessage = message ?? $"{paramName} must be within: [{min}, {max}]";

            if (min != int.MinValue && max != int.MaxValue)
            {
                // check both bounds
                invalid |= min <= value && value <= max;
            }
            else if (min != int.MinValue)
            {
                // check lower bound
                invalid |= min <= value;
            }
            else
            {
                // check upper bound
                invalid |= value <= max;
            }

            if (invalid)
            {
                throw new ArgumentOutOfRangeException(paramName, value, exMessage);
            }
        }

        [DebuggerHidden]
        public static T IsNotOfType<T>(object value, string paramName)
        {
            if (value is not T result)
            {
                throw new InvalidCastException($"Cannot cast {paramName} to type {nameof(T)}");
            }

            return result;
        }
    }
}
