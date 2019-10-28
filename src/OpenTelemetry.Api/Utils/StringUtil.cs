﻿// <copyright file="StringUtil.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Utils
{
    internal static class StringUtil
    {
        public static bool IsPrintableString(string str)
        {
            for (var i = 0; i < str.Length; i++)
            {
                if (!IsPrintableChar(str[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPrintableChar(char ch)
        {
            return ch >= ' ' && ch <= '~';
        }
    }
}
