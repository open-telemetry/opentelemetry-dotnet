using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Utils.Messaging;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RabbitMqController : ControllerBase
    {
        private readonly ILogger<RabbitMqController> logger;
        private readonly MessageSender messageSender;

        public RabbitMqController(ILogger<RabbitMqController> logger, MessageSender messageSender)
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
