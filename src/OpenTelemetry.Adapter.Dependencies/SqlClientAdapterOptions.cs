// <copyright file="SqlClientAdapterOptions.cs" company="OpenTelemetry Authors">
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
using System.Data;

namespace OpenTelemetry.Adapter.Dependencies
{
    /// <summary>
    /// Options for <see cref="SqlClientAdapter"/>.
    /// </summary>
    public class SqlClientAdapterOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlClientAdapterOptions"/> class.
        /// </summary>
        public SqlClientAdapterOptions()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientAdapter"/> should capture the names of <see cref="CommandType.StoredProcedure"/> commands. Default value: True.
        /// </summary>
        public bool CaptureStoredProcedureCommandName { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientAdapter"/> should capture the text of <see cref="CommandType.Text"/> commands. Default value: False.
        /// </summary>
        public bool CaptureTextCommandContent { get; set; }
    }
}
