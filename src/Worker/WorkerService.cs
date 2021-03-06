using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Worker
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConnectionFactory _connectionFactory;
        private readonly IConnection _rabbitConnection;
        private readonly IModel _rabbitChanel;
        private readonly TextMapPropagator _propagator;
        private readonly ActivitySource _activitySource;

        public WorkerService(
            ILogger<WorkerService> logger,
            IConfiguration configuration,
            ConnectionFactory connectionFactory,
            ActivitySource activitySource)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionFactory = connectionFactory;

            _rabbitConnection = _connectionFactory.CreateConnection();
            _rabbitChanel = _rabbitConnection.CreateModel();
            _propagator = new TraceContextPropagator();
            _activitySource = activitySource;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _rabbitChanel.QueueDeclare(
                queue: _configuration["RabbitMq:QueueName"],
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            RabbitMqHelper.StartConsumer(_rabbitChanel, _configuration, MessageHandler);

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public void MessageHandler(BasicDeliverEventArgs eventArgs)
        {
            // Extract the PropagationContext from the upstream service using message headers.
            var parentContext = _propagator.Extract(
                default,
                eventArgs.BasicProperties,
                RabbitMqHelper.ExtractTraceContextFromBasicProperties);
            Baggage.Current = parentContext.Baggage;

            using (var activity = _activitySource.StartActivity(
                _configuration["Zipkin:AppName"],
                ActivityKind.Consumer,
                parentContext.ActivityContext))
            {
                var body = eventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received {0}", message);
                _logger.LogInformation("Traceparent: {0}", Activity.Current.Id);
                _logger.LogInformation("Tracestate: {0}", Activity.Current.TraceStateString);

                activity.SetTag("message", message);
                RabbitMqHelper.AddMessagingTags(activity, _configuration);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping queue listener...");
            _rabbitChanel.Dispose();
            _rabbitConnection.Dispose();
            await base.StopAsync(stoppingToken);
        }
    }
}
