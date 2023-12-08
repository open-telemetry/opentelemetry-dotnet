// <copyright file="TestMiddleware.cs" company="OpenTelemetry Authors">
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

namespace TestApp.AspNetCore;

public static class TestMiddleware
{
    private static readonly AsyncLocal<Action<IApplicationBuilder>?> Current = new();

    public static IApplicationBuilder AddTestMiddleware(this IApplicationBuilder builder)
    {
        if (Current.Value is { } configure)
        {
            configure(builder);
        }

        return builder;
    }

    public static void Create(Action<IApplicationBuilder> action)
    {
        Current.Value = action;
    }
}
