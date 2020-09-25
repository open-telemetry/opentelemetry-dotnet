// <copyright file="SkipUnlessEnvVarFoundTheoryAttribute.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Tests
{
    internal class SkipUnlessEnvVarFoundTheoryAttribute : TheoryAttribute
    {
        public SkipUnlessEnvVarFoundTheoryAttribute(string environmentVariable)
        {
            if (string.IsNullOrEmpty(GetEnvironmentVariable(environmentVariable)))
            {
                this.Skip = $"Skipped because {environmentVariable} environment variable was not configured.";
            }
        }

        public static string GetEnvironmentVariable(string environmentVariableName)
        {
            string environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(environmentVariableValue))
            {
                environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
            }

            return environmentVariableValue;
        }
    }
}
