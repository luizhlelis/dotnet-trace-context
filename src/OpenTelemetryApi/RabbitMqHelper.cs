using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace OpenTelemetryApi
{
    public static class RabbitMqHelper
    {
        public static void InjectTraceContextIntoBasicProperties(
            IBasicProperties props, string key, string value)
        {
            if (props.Headers == null)
            {
                props.Headers = new Dictionary<string, object>();
            }

            props.Headers[key] = value;
        }

        public static void AddMessagingTags(Activity activity, IConfiguration configuration)
        {
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.destination", "worker");
            activity?.SetTag("messaging.rabbitmq.routing_key", configuration["RabbitMq:QueueName"]);
        }
    }
}
