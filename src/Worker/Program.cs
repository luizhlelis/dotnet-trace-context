using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IConfigurationRoot BuildAppConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            return configuration.AddEnvironmentVariables().Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = BuildAppConfiguration();

                    services.AddHostedService<WorkerBackgroundService>();

                    //var factory = new ConnectionFactory
                    //{
                    //    Uri = new Uri(configuration["RabbitMQ:Url"])
                    //};
                    //services.AddSingleton(factory);

                    //var rabbitMqSection = configuration.GetSection("RabbitMq");
                    //var exchangeSection = configuration.GetSection("RabbitMqExchange");

                    services.AddOpenTelemetryTracing(config => config
                        .AddZipkinExporter(o =>
                        {
                            o.Endpoint = new Uri(configuration["Zipkin:Url"]);
                        })
                    );

                });
    }
}
