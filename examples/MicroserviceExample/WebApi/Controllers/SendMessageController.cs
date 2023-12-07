// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using Utils.Messaging;

namespace WebApi.Controllers;

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