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

namespace HttpServerExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webserver = new WebServer();

            webserver.Start();

            var client = new Client();

            Console.WriteLine("Type \"send {name}\" to send. Type \"exit\" to exit.");
            while (true)
            {
                var cmdLine = Console.ReadLine();
                if (cmdLine != null)
                {
                    var cmds = cmdLine.Split(" ");

                    if (cmds[0] == "exit")
                    {
                        break;
                    }
                    else if (cmds[0] =="send")
                    {
                        var names = cmds.AsSpan<string>().Slice(1);
                        var name = String.Join("/", names.ToArray());

                        var t = client.SendRequest(name);
                    }
                }
            }

            webserver.Stop();
        }
    }
}
