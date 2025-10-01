// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using Utils.Messaging;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class SendMessageController : ControllerBase
{
    private readonly MessageSender messageSender;

    public SendMessageController(MessageSender messageSender)
    {
        this.messageSender = messageSender;
    }

    [HttpGet]
    public async Task<string> Get() =>
        await this.messageSender.SendMessageAsync().ConfigureAwait(false);
}
