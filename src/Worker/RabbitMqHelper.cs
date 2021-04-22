using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Worker
{
    public static class RabbitMqHelper
    {
        public static void StartConsumer(
            IModel rabbitChanel,
            IConfiguration configuration,
            Action<BasicDeliverEventArgs> messageHandler)
        {
            var rabbitEventConsumer = new EventingBasicConsumer(rabbitChanel);

            rabbitEventConsumer.Received += (model, eventArgs) => messageHandler(eventArgs);

            rabbitChanel.BasicConsume(
                queue: configuration["RabbitMq:QueueName"],
                autoAck: true,
                consumer: rabbitEventConsumer);
        }

        public static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return new[] { Encoding.UTF8.GetString(bytes) };
            }

            return Enumerable.Empty<string>();
        }

        public static void AddMessagingTags(Activity activity, IConfiguration configuration)
        {
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.destination", "");
            activity?.SetTag("messaging.rabbitmq.routing_key", configuration["RabbitMq:QueueName"]);
        }
    }
}
