// <copyright file="EntityFrameworkInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

using System.Data;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore
{
    /// <summary>
    /// Options for <see cref="EntityFrameworkInstrumentation"/>.
    /// </summary>
    public class EntityFrameworkInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="EntityFrameworkInstrumentation"/> should add the names of <see cref="CommandType.StoredProcedure"/> commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: True.
        /// </summary>
        public bool SetStoredProcedureCommandName { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="EntityFrameworkInstrumentation"/> should add the text of <see cref="CommandType.Text"/> commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: False.
        /// </summary>
        public bool SetTextCommandContent { get; set; } = false;
    }
}
