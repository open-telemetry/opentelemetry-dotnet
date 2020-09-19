// <copyright file="SendMessageController.cs" company="OpenTelemetry Authors">
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Utils.Messaging;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SendMessageController : ControllerBase
    {
        private readonly ILogger<SendMessageController> logger;
        private readonly MessageSender messageSender;

        public SendMessageController(ILogger<SendMessageController> logger, MessageSender messageSender)
        {
            this.logger = logger;
            this.messageSender = messageSender;
        }

        [HttpGet]
        public string Get()
        {
            return this.messageSender.SendMessage();
        }
    }
}
