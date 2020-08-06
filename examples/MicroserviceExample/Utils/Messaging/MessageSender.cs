﻿// <copyright file="MessageSender.cs" company="OpenTelemetry Authors">
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
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Utils.Messaging
{
    public class MessageSender : IDisposable
    {
        private readonly ILogger<MessageSender> logger;
        private readonly IConnection connection;
        private readonly IModel channel;

        private static readonly ActivitySource activitySource = new ActivitySource(nameof(MessageSender));
        private static readonly ITextFormat TextFormat = new TraceContextFormat();

        public MessageSender(ILogger<MessageSender> logger)
        {
            this.logger = logger;
            this.connection = RabbitMqHelper.CreateConnection();
            this.channel = RabbitMqHelper.CreateModelAndDeclareTestQueue(this.connection);
        }

        public void Dispose()
        {
            this.channel.Dispose();
            this.connection.Dispose();
        }

        public string SendMessage()
        {
            try
            {
                // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
                // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/messaging.md#span-name
                var activityName = $"{RabbitMqHelper.TestQueueName} send";

                using (var activity = activitySource.StartActivity(activityName, ActivityKind.Producer))
                {
                    var props = channel.CreateBasicProperties();

                    // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
                    TextFormat.Inject(activity.Context, props, InjectTraceContextIntoBasicProperties);

                    // The OpenTelemetry messaging specification defines a number of attributes. These attributes are added here.
                    RabbitMqHelper.AddMessagingTags(activity);

                    var body = $"Published message: DateTime.Now = {DateTime.Now}.";

                    channel.BasicPublish(
                        exchange: RabbitMqHelper.DefaultExchangeName,
                        routingKey: RabbitMqHelper.TestQueueName,
                        basicProperties: props,
                        body: Encoding.UTF8.GetBytes(body));

                    this.logger.LogInformation($"Message sent: [{body}]");

                    return body;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Message publishing failed.");
                throw;
            }
        }

        private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
        {
            try
            {
                if (props.Headers == null)
                {
                    props.Headers = new Dictionary<string, object>();
                }

                props.Headers[key] = value;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to inject trace context.");
            }
        }
    }
}
