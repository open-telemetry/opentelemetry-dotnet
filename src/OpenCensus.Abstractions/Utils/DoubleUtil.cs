// <copyright file="DoubleUtil.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Utils
{
    using System;

    internal static class DoubleUtil
    {
        public static long ToInt64(double arg)
        {
            if (double.IsPositiveInfinity(arg))
            {
                return 0x7ff0000000000000L;
            }

            if (double.IsNegativeInfinity(arg))
            {
                unchecked
                {
                    return (long)0xfff0000000000000L;
                }
            }

            if (double.IsNaN(arg))
            {
                return 0x7ff8000000000000L;
            }

            if (arg == double.MaxValue)
            {
                return long.MaxValue;
            }

            if (arg == double.MinValue)
            {
                return long.MinValue;
            }

            return Convert.ToInt64(arg);
        }
    }
}
