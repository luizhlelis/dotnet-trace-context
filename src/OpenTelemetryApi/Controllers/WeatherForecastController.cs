using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;

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
        private readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

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
            _logger.LogInformation("Traceparent: {0}", Activity.Current.Id);
            _logger.LogInformation("Tracestate: {0}", Activity.Current.TraceStateString);
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonConvert.SerializeObject(weatherForecast), Encoding.UTF8, "application/json");
            await client.PostAsync(_configuration["ClientUrl"], content);

            return Ok();
        }

        [HttpPost]
        [Route("PublishInQueue")]
        public IActionResult PublishInQueue([FromBody] WeatherForecast weatherForecast)
        {
            var message = JsonConvert.SerializeObject(weatherForecast);
            var body = Encoding.UTF8.GetBytes(message);
            var traceparent = Activity.Current.Id;
            var tracestate = Activity.Current.TraceStateString;
            _logger.LogInformation("Traceparent: {0}", traceparent);
            _logger.LogInformation("Tracestate: {0}", tracestate);

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(
                        queue: _configuration["RabbitMq:QueueName"],
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    var basicProps = channel.CreateBasicProperties();

                    // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
                    var contextToInject = Activity.Current.Context;
                    _propagator.Inject(
                        new PropagationContext(contextToInject, Baggage.Current),
                        basicProps,
                        RabbitMqHelper.InjectTraceContextIntoBasicProperties);

                    RabbitMqHelper.AddMessagingTags(Activity.Current, _configuration);

                    channel.BasicPublish(
                        exchange: "",
                        routingKey: _configuration["RabbitMq:QueueName"],
                        basicProperties: basicProps,
                        body: body);
                }
            }

            return Ok();
        }
    }
}
