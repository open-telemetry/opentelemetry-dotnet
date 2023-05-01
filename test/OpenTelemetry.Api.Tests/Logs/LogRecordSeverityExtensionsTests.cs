// <copyright file="LogRecordSeverityExtensionsTests.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordSeverityExtensionsTests
{
    [Theory]
    [InlineData(0, LogRecordSeverityExtensions.UnspecifiedShortName)]
    [InlineData(int.MinValue, LogRecordSeverityExtensions.UnspecifiedShortName)]
    [InlineData(int.MaxValue, LogRecordSeverityExtensions.UnspecifiedShortName)]
    [InlineData(1, LogRecordSeverityExtensions.TraceShortName)]
    [InlineData(2, LogRecordSeverityExtensions.Trace2ShortName)]
    [InlineData(3, LogRecordSeverityExtensions.Trace3ShortName)]
    [InlineData(4, LogRecordSeverityExtensions.Trace4ShortName)]
    [InlineData(5, LogRecordSeverityExtensions.DebugShortName)]
    [InlineData(6, LogRecordSeverityExtensions.Debug2ShortName)]
    [InlineData(7, LogRecordSeverityExtensions.Debug3ShortName)]
    [InlineData(8, LogRecordSeverityExtensions.Debug4ShortName)]
    [InlineData(9, LogRecordSeverityExtensions.InfoShortName)]
    [InlineData(10, LogRecordSeverityExtensions.Info2ShortName)]
    [InlineData(11, LogRecordSeverityExtensions.Info3ShortName)]
    [InlineData(12, LogRecordSeverityExtensions.Info4ShortName)]
    [InlineData(13, LogRecordSeverityExtensions.WarnShortName)]
    [InlineData(14, LogRecordSeverityExtensions.Warn2ShortName)]
    [InlineData(15, LogRecordSeverityExtensions.Warn3ShortName)]
    [InlineData(16, LogRecordSeverityExtensions.Warn4ShortName)]
    [InlineData(17, LogRecordSeverityExtensions.ErrorShortName)]
    [InlineData(18, LogRecordSeverityExtensions.Error2ShortName)]
    [InlineData(19, LogRecordSeverityExtensions.Error3ShortName)]
    [InlineData(20, LogRecordSeverityExtensions.Error4ShortName)]
    [InlineData(21, LogRecordSeverityExtensions.FatalShortName)]
    [InlineData(22, LogRecordSeverityExtensions.Fatal2ShortName)]
    [InlineData(23, LogRecordSeverityExtensions.Fatal3ShortName)]
    [InlineData(24, LogRecordSeverityExtensions.Fatal4ShortName)]
    public void ToShortNameTest(int logRecordSeverityValue, string expectedName)
    {
        var logRecordSeverity = (LogRecordSeverity)logRecordSeverityValue;

        Assert.Equal(expectedName, logRecordSeverity.ToShortName());
    }
}
