// <copyright file="ShimExtensions.cs" company="OpenTelemetry Authors">
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

#if NETSTANDARD2_0 || NET461
namespace System
{
    internal static class ShimExtensions
    {
        public static byte[] ToArray(this ArraySegment<byte> arraySegment)
        {
            int count = arraySegment.Count;
            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            var array = new byte[count];
            Array.Copy(arraySegment.Array, arraySegment.Offset, array, 0, count);
            return array;
        }
    }
}
#endif
