// <copyright file="Client.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpServerExample
{
    public class Client
    {
        public static Task StartClientTask(string url, int periodMilli, CancellationToken token)
        {
            Task task = Task.Run(async () =>
            {
                Console.WriteLine("Client Started.");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        HttpClient client = new HttpClient();

                        HttpResponseMessage response = await client.GetAsync(url);

                        response.EnsureSuccessStatusCode();

                        string responseBody = await response.Content.ReadAsStringAsync();

                        Console.WriteLine($"Client got \"{responseBody}\"");
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine("Client Exception: {0}", e.Message);
                    }

                    try
                    {
                        await Task.Delay(periodMilli, token);
                    }
                    catch (Exception)
                    {
                        // Do Nothing
                    }
                }

                Console.WriteLine("Client Stopped.");
            });

            return task;
        }
    }
}
