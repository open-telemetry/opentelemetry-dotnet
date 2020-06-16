﻿// <copyright file="DateTimeOffsetExtensions.net452.cs" company="OpenTelemetry Authors">
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
#if NET452
using System;

namespace OpenTelemetry.Exporter.Prometheus
{
    internal static class DateTimeOffsetExtensions
    {
        private const int DaysPerYear = 365;
        private const int DaysPer4Years = (DaysPerYear * 4) + 1;       // 1461
        private const int DaysPer100Years = (DaysPer4Years * 25) - 1;  // 36524
        private const int DaysPer400Years = (DaysPer100Years * 4) + 1; // 146097
        private const int DaysTo1970 = (DaysPer400Years * 4) + (DaysPer100Years * 3) + (DaysPer4Years * 17) + DaysPerYear; // 719,162
        private const long UnixEpochTicks = TimeSpan.TicksPerDay * DaysTo1970; // 621,355,968,000,000,000
        private const long UnixEpochMilliseconds = UnixEpochTicks / TimeSpan.TicksPerMillisecond; // 62,135,596,800,000

        public static long ToUnixTimeMilliseconds(this DateTimeOffset dateTimeOffset)
        {
            // Truncate sub-millisecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long milliseconds = dateTimeOffset.Ticks / TimeSpan.TicksPerMillisecond;
            return milliseconds - UnixEpochMilliseconds;
        }
    }
}
#endif
