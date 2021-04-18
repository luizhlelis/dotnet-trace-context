using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Worker
{
    public class WorkerBackgroundService : BackgroundService
    {
        private readonly ILogger<WorkerBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConnectionFactory _connectionFactory;
        private readonly IConnection _rabbitConnection;
        private readonly IModel _rabbitChanel;
        private EventingBasicConsumer RabbitEventConsumer;

        public WorkerBackgroundService(
            ILogger<WorkerBackgroundService> logger,
            IConfiguration configuration,
            ConnectionFactory connectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionFactory = connectionFactory;

            _rabbitConnection = _connectionFactory.CreateConnection();
            _rabbitChanel = _rabbitConnection.CreateModel();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _rabbitChanel.QueueDeclare(
                queue: _configuration["RabbitMq:QueueName"],
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            RabbitEventConsumer = new EventingBasicConsumer(_rabbitChanel);

            RabbitEventConsumer.Received += (model, eventArgs) =>
            {
                var messageHandlingActivity = new Activity(_configuration["Zipkin:AppName"]);
                messageHandlingActivity.Start();

                var body = eventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation(" [x] Received {0}", message);
                _logger.LogInformation(">>>>>>>>>>>>> Traceparent: {0}", Activity.Current.Id);

                messageHandlingActivity.Stop();
            };

            _rabbitChanel.BasicConsume(
                queue: _configuration["RabbitMq:QueueName"],
                autoAck: true,
                consumer: RabbitEventConsumer);

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
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
