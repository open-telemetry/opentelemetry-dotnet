// <copyright file="LogConverter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Represents a method that converts a <see cref="LogRecordStruct"/> log
    /// message into another type.
    /// </summary>
    /// <typeparam name="T">The type the <see cref="LogRecordStruct"/> log
    /// message is converted to.</typeparam>
    /// <param name="log">The <see cref="LogRecordStruct"/> log message to
    /// convert.</param>
    /// <returns>The <typeparamref name="T"/> which represents the log
    /// message.</returns>
    public delegate T LogConverter<T>(in LogRecordStruct log);
}
