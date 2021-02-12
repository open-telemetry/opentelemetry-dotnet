// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

namespace HttpServerExample
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            CancellationTokenSource cancelSrc = new CancellationTokenSource();

            var client1Task = Client.StartClientTask("http://127.0.0.1:3000/Test1", 1000, cancelSrc.Token);
            var client2Task = Client.StartClientTask("http://127.0.0.1:3000/Test2", 5000, cancelSrc.Token);


            var webserver = new WebServer();
            var webserverTask = webserver.StartServerTask("http://127.0.0.1:3000/", cancelSrc.Token);

            Console.WriteLine("Press [ENTER] to exit.");
            var cmdline = Console.ReadLine();
            cancelSrc.Cancel();

            webserver.Shutdown();
            await webserverTask;
        }
    }
}
