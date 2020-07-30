using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RabbitMqController : ControllerBase
    {
        private readonly ILogger<RabbitMqController> logger;
        private readonly RabbitMqService rabbitMqService;

        public RabbitMqController(ILogger<RabbitMqController> logger, RabbitMqService rabbitMqService)
        {
            this.logger = logger;
            this.rabbitMqService = rabbitMqService;
        }

        [HttpGet]
        public string Get()
        {
            return this.rabbitMqService.PublishMessage();
        }
    }
}
