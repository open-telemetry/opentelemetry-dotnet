// <copyright file="Arrays.cs" company="OpenCensus Authors">
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
    using System.Text;

    internal static class Arrays
    {
        private static readonly uint[] ByteToHexLookupTable = CreateLookupTable();

        public static bool Equals(byte[] array1, byte[] array2)
        {
            if (array1 == array2)
            {
                return true;
            }

            if (array1 == null || array2 == null)
            {
                return false;
            }

            if (array2.Length != array1.Length)
            {
                return false;
            }

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal static int GetHashCode(byte[] array)
        {
            if (array == null)
            {
                return 0;
            }

            int result = 1;
            foreach (byte b in array)
            {
                result = (31 * result) + b;
            }

            return result;
        }

        internal static int HexCharToInt(char c)
        {
            if ((c >= '0') && (c <= '9'))
            {
                return c - '0';
            }

            if ((c >= 'a') && (c <= 'f'))
            {
                return c - 'a' + 10;
            }

            if ((c >= 'A') && (c <= 'F'))
            {
                return c - 'A' + 10;
            }

            throw new ArgumentOutOfRangeException("Invalid character: " + c);
        }

        // https://stackoverflow.com/a/24343727
        internal static uint[] CreateLookupTable()
        {
            uint[] table = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("x2");
                table[i] = (uint)s[0];
                table[i] += (uint)s[1] << 16;
            }

            return table;
        }

        // https://stackoverflow.com/a/24343727
        internal static char[] ByteToHexCharArray(byte b)
        {
            char[] result = new char[2];

            result[0] = (char)ByteToHexLookupTable[b];
            result[1] = (char)(ByteToHexLookupTable[b] >> 16);

            return result;
        }

        internal static byte[] StringToByteArray(string src, int start = 0, int len = -1)
        {
            if (len == -1)
            {
                len = src.Length;
            }

            int size = len / 2;
            byte[] bytes = new byte[size];
            for (int i = 0, j = start; i < size; i++)
            {
                int high = HexCharToInt(src[j++]);
                int low = HexCharToInt(src[j++]);
                bytes[i] = (byte)(high << 4 | low);
            }

            return bytes;
        }

        internal static string ByteArrayToString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(ByteToHexCharArray(bytes[i]));
            }

            return sb.ToString();
        }
    }
}
