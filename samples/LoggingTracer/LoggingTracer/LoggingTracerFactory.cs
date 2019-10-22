// <copyright file="LoggingTracerFactory.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public class LoggingTracerFactory : TracerFactoryBase
    {
        /// <inheritdoc/>
        public override ITracer GetTracer(string name, string version = null)
        {
            Logger.Log($"ITracerFactory.GetTracer('{name}', '{version}')");

            // Create a Resource from "name" and "version" information.
            var labels = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(name))
            {
                labels.Add("name", name);
                if (!string.IsNullOrEmpty(version))
                {
                    labels.Add("version", version);
                }
            }

            return new LoggingTracer($"{name}:{version}");
        }
    }
}
