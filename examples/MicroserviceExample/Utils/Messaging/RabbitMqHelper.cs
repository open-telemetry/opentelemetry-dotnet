// <copyright file="RabbitMqHelper.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Utils.Messaging
{
    public static class RabbitMqHelper
    {
        public const string DefaultExchangeName = "";
        public const string TestQueueName = "TestQueue";

        private static readonly ConnectionFactory ConnectionFactory;

        static RabbitMqHelper()
        {
            ConnectionFactory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost",
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? "guest",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? "guest",
                Port = 5672,
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(3000),
            };
        }

        public static IConnection CreateConnection()
        {
            return ConnectionFactory.CreateConnection();
        }

        public static IModel CreateModelAndDeclareTestQueue(IConnection connection)
        {
            var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: TestQueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            return channel;
        }

        public static void StartConsumer(IModel channel, Action<BasicDeliverEventArgs> processMessage)
        {
            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (bc, ea) => processMessage(ea);

            channel.BasicConsume(queue: TestQueueName, autoAck: true, consumer: consumer);
        }

        public static void AddMessagingTags(Activity activity)
        {
            // These tags are added demonstrating the semantic conventions of the OpenTelemetry messaging specification
            // See:
            //   * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#messaging-attributes
            //   * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#rabbitmq
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.destination", DefaultExchangeName);
            activity?.SetTag("messaging.rabbitmq.routing_key", TestQueueName);
        }
    }
}
