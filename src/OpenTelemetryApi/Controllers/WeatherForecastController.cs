using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Newtonsoft.Json;
using System.Text;

namespace OpenTelemetryApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ConnectionFactory _connectionFactory;

        public WeatherForecastController(
            ILogger<WeatherForecastController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ConnectionFactory connectionFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _connectionFactory = connectionFactory;
        }

        [HttpPost]
        public async Task<IActionResult> SendToTheOtherApi([FromBody] WeatherForecast weatherForecast)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _configuration["ClientUrl"]);
            //request.Headers.Add("Accept", "application/vnd.github.v3+json");
            //request.Headers.Add("User-Agent", "HttpClientFactory-Sample");

            var client = _httpClientFactory.CreateClient();
            await client.SendAsync(request);

            return Ok();
        }

        [HttpPost]
        [Route("[controller]/PublishInQueue")]
        public IActionResult PublishInQueue([FromBody] WeatherForecast weatherForecast)
        {
            var message = JsonConvert.SerializeObject(weatherForecast);
            var body = Encoding.UTF8.GetBytes(message);

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: _configuration["RabbitMQ:QueueName"], durable: false,
                        exclusive: false, autoDelete: false, arguments: null);

                    channel.BasicPublish(exchange: "", routingKey: "hello", basicProperties: null, body: body);
                }
            }

            return Ok();
        }
    }
}
