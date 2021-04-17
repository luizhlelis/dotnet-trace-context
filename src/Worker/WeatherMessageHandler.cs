using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Core.DependencyInjection;
using RabbitMQ.Client.Core.DependencyInjection.MessageHandlers;
using RabbitMQ.Client.Events;

namespace Worker
{
    public class WeatherMessageHandler : IMessageHandler
    {
        private readonly ILogger<WeatherMessageHandler> _logger;
        public WeatherMessageHandler(ILogger<WeatherMessageHandler> logger)
        {
            _logger = logger;
        }

        public void Handle(BasicDeliverEventArgs eventArgs, string matchingRoute)
        {
            _logger.LogInformation($"Handling message {eventArgs.GetMessage()} by routing key {matchingRoute}");
        }
    }
}