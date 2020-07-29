// <copyright file="TestRedis.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Examples.Console
{
    internal class TestRedis
    {
        internal static object Run(string zipkinUri)
        {
            /*
             * Setup redis service inside local docker.
             * docker run --name opentelemetry-redis-test -d -p 6379:6379 redis
             *
             * If you face any issue with the first command, do the following ones:
             * docker exec -it opentelemetry-redis-test sh
             * redis-cli
             * set bind 0.0.0.0
             * save
             */

            // connect to the redis server. The default port 6379 will be used.
            var connection = ConnectionMultiplexer.Connect("localhost");

            // Configure exporter to export traces to Zipkin
            using var openTelemetry = Sdk.CreateTracerProvider(
                builder => builder
                    .UseZipkinExporter(o =>
                    {
                        o.ServiceName = "redis-test";
                        o.Endpoint = new Uri(zipkinUri);
                    })
                    .AddRedisInstrumentation(connection, options =>
                    {
                        // changing flushinterval from 10s to 5s
                        options.FlushInterval = TimeSpan.FromSeconds(5);
                    })
                    .AddActivitySource("redis-test"));

            ActivitySource activitySource = new ActivitySource("redis-test");

            // select a database (by default, DB = 0)
            var db = connection.GetDatabase();

            // Create a scoped activity. It will end automatically when using statement ends
            using (activitySource.StartActivity("Main"))
            {
                System.Console.WriteLine("About to do a busy work");
                for (var i = 0; i < 10; i++)
                {
                    DoWork(db, activitySource);
                }
            }

            return null;
        }

        private static void DoWork(IDatabase db, ActivitySource activitySource)
        {
            // Start another activity. If another activity was already started, it'll use that activity as the parent activity.
            // In this example, the main method already started a activity, so that'll be the parent activity, and this will be
            // a child activity.
            using (Activity activity = activitySource.StartActivity("DoWork"))
            {
                try
                {
                    db.StringSet("key", "value " + DateTime.Now.ToLongDateString());

                    System.Console.WriteLine("Doing busy work");
                    Thread.Sleep(1000);

                    // run a command, in this case a GET
                    var myVal = db.StringGet("key");

                    System.Console.WriteLine(myVal);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    // Set status upon error
                    activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(Status.Internal.CanonicalCode));
                    activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, e.ToString());
                }

                // Annotate our activity to capture metadata about our operation
                var attributes = new Dictionary<string, object>
                {
                    { "use", "demo" },
                };
                activity.AddEvent(new ActivityEvent("Invoking DoWork", attributes));
            }
        }
    }
}
