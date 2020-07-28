using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RabbitMqController : ControllerBase
    {
        private readonly ILogger<RabbitMqController> _logger;
        private readonly IRabbitMqService _rabbitMqService;

        public RabbitMqController(ILogger<RabbitMqController> logger, IRabbitMqService rabbitMqService)
        {
            _logger = logger;
            _rabbitMqService = rabbitMqService;
        }

        [HttpGet]
        public string Get()
        {
            return _rabbitMqService.PublishMessage();
        }
    }
}
