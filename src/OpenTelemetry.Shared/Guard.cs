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
                // TODO: say it is null or empty?
                throw new ArgumentNullException(paramName);
            }
        }

        [DebuggerHidden]
        public static void IsNotNullOrWhitespace(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // TODO: say it is null or whitespace?
                throw new ArgumentNullException(paramName);
            }
        }

        [DebuggerHidden]
        public static void IsNotZero(int value, string paramName, string message)
        {
            if (value == 0)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        [DebuggerHidden]
        public static void IsNotValidTimeout(int value, string paramName)
        {
            if (value < 0 && value != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be non-negative or Timeout.Infinite");
            }
        }

    }
}
