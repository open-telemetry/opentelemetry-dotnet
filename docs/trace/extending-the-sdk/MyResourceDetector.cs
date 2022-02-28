// <copyright file="MyResourceDetector.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Resources;

internal class MyResourceDetector : IResourceDetector
{
    public const string EnVarkey = "myEnVarkey";

    public Resource Detect()
    {
        var resource = Resource.Empty;
        if (this.LoadString(EnVarkey, out string envAttributeVal))
        {
            resource = new Resource(new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>(EnVarkey, envAttributeVal),
            });
        }

        return resource;
    }

    internal bool LoadString(string envVarKey, out string result)
    {
        result = null;

        try
        {
            result = Environment.GetEnvironmentVariable(envVarKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: {0}.", ex);
            return false;
        }

        return !string.IsNullOrEmpty(result);
    }
}
