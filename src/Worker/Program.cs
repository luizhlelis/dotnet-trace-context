using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

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

                    services.AddSingleton(new ConnectionFactory
                    {
                        HostName = configuration["RabbitMq:HostName"],
                        Port = configuration.GetValue<int>("RabbitMq:Port"),
                        UserName = configuration["RabbitMq:UserName"],
                        Password = configuration["RabbitMq:Password"],
                        VirtualHost = configuration["RabbitMq:VirtualHost"]
                    });

                    services.AddOpenTelemetryTracing(config => config
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(configuration["Zipkin:AppName"]))
                        .AddZipkinExporter(o =>
                        {
                            o.Endpoint = new Uri(configuration["Zipkin:Url"]);
                        })
                        .AddAspNetCoreInstrumentation()
                    );

                });
    }
}
