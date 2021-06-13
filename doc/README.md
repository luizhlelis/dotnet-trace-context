# [c#] Using W3C Trace Context standard in distributed tracing

In my last [article](https://dev.to/luizhlelis/using-w3c-trace-context-standard-in-distributed-tracing-3743), I wrote about the W3C trace context standard and what kind of problem it came to solve. The current article purpose is show the trace context usage in a microservice architecture. For the first practical example, I chose to develop all applications using c# with `.NET 5` ([sample WeatherForecast web API](https://docs.microsoft.com/aspnet/core/tutorials/first-web-api?view=aspnetcore-5.0&tabs=visual-studio)) and run all of them locally via docker-compose. Hope you enjoy it!

## Application architecture

The main objective is to propagate a message with `traceparent` id throw two api's and one worker using [W3C trace context](https://www.w3.org/TR/trace-context) standard. The `first-api` calls the `second-api` by a http call, on the other hand, the `second-api` has an asynchronous communication with the `worker` by a message broker ([rabbitmq](https://www.rabbitmq.com/) was chosen for that). Furthermore, [zipkin](https://zipkin.io/) was the trace system chosen (or `vendor` as the standard call it), being responsible for get the application traces and build the distributed tracing diagram:

### <a name="firstfigure"></a>Figure 1 - Distributed trace

![Distributed Trace](w3c-trace-context.png)

the first and second APIs have the [same code base](../src/OpenTelemetryApi), but they're deployed in different containers.

## OpenTelemetry

An important framework used in the present article to deal with the different traces is [OpenTelemetry](https://opentelemetry.io/). As the documentation saids:

> OpenTelemetry is a set of APIs, SDKs, tooling and integrations that are designed for the creation and management of telemetry data such as traces, metrics, and logs.

[OTel](https://opentelemetry.io/docs/concepts/glossary/) provides a vendor-agnostic instrumentation library to generate, emit, collect, process and export telemetry data. That's not only the only purpose of `OTel`, which is composed by multiple components: proto, specification, collector, instrumentation libraries; but that's subject for other article.

`W3C TraceContext` is one of the [propagators](https://github.com/open-telemetry/opentelemetry-specification/blob/b46bcab5fb709381f1fd52096a19541370c7d1b3/specification/context/api-propagators.md#propagators-distribution) maintained and distributed as extension packages by `OTel`. That's the reason why `OTel` is always related to `W3C TraceContext` and vice versa.

## Talk is cheap, show me the code

> The source code could be found in [this github repo](https://github.com/luizhlelis/dotnet-trace-context).

The default diagnostics library in `.NET 5`, called [System.Diagnostics](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics?view=net-5.0), is already prepared to propagate the context based on W3C TraceContext specification. In previous `.NET Core` versions, the context was propagated with an [hierarchical identifier format](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md#id-format) by default. On `.NET Core 3.0`, the identifier format setup started to be available, see [this](https://stackoverflow.com/questions/61251914/how-can-i-access-w3c-tracecontext-headers-in-a-net-core-3-1-application/67086305#67086305) stackoverflow question for more information about how to configure w3c's format in previous `.NET Core` versions.

The `first-api` and the `second-api` showed in [Figure 1](#firstfigure) requires three packages to work properly with `OpenTelemetry`:

``` csharp
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.0.0-rc2" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.0.0-rc2" />
    <PackageReference Include="OpenTelemetry.Exporter.Zipkin" Version="1.0.1" />
```

the `OpenTelemetry.Extensions.Hosting` package is responsible for register `OpenTelemetry` into the application using Dependency Injection, the `OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Exporter.Zipkin` packages represents two source components of `OpenTelemetry` framework: the instrumentation library and the collector respectively. The [instrumentation library](https://opentelemetry.io/docs/concepts/instrumenting/) is responsible to inject the observable information from libraries and applications into the OpenTelemetry API. On the other hand, the [collector](https://opentelemetry.io/docs/concepts/data-collection/) offers a vendor-agnostic implementation on how to receive, process, and export telemetry data, the exporter specifically is the place where to send the received data (`zipkin` was the chosen for our example). The `OTel`'s dependency injection was done in `Startup.cs`:

``` csharp
    services.AddOpenTelemetryTracing(builder => builder
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Configuration["Zipkin:AppName"]))
        .AddZipkinExporter(o =>
            {
                o.Endpoint = new Uri(Configuration["Zipkin:Url"]);
            })
        .AddAspNetCoreInstrumentation()
    );
```

As mentioned before, the `first-api` and the `second-api` have the [same code base](../src/OpenTelemetryApi). For this example, the first is called by a client (`curl`) in `WeatherForecast` route which calls the second one in the `PublishInQueue` route. Both controller methods have a stdout print for `traceparent` and `tracestate`:

``` csharp
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
            _logger.LogInformation("Traceparent: {0}", traceparent);
            _logger.LogInformation("Tracestate: {0}", Activity.Current.TraceStateString);

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

                    basicProps.Headers = new Dictionary<string, object>();
                    basicProps.Headers.TryAdd("traceparent", traceparent);
                    Activity.Current.SetTag("messaging.system", "rabbitmq");
                    Activity.Current.SetTag("messaging.destination_kind", "queue");
                    Activity.Current.SetTag("messaging.destination", "");
                    Activity.Current.SetTag("messaging.rabbitmq.routing_key", _configuration["RabbitMq:QueueName"]);

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
```

By default, the `ASP.NET core` starts an `Activity` span when the [request is beginning](https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L59) and stop it [at the end](https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L156) so this kind of setup was not required for the `first-api` and the `second-api`. On the other hand, the manually creation of an activity span is required for the `worker` because that kind of span is outside from http calls, is not an API, but it's a message listener.

> **_NOTE:_** `ASP.NET core` also sets the `traceparent` from the upstream request as [the current activity ParentId](https://github.com/dotnet/aspnetcore/blob/main/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L289).

For the `worker` those packages are required:

``` csharp
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.0.0-rc2" />
    <PackageReference Include="OpenTelemetry.Exporter.Zipkin" Version="1.0.1" />
```

and the `OTel`'s dependency injection was configured to the `worker` in `Program.cs` like the following bellow:

``` csharp
    services.AddOpenTelemetryTracing(config => config
        .SetResourceBuilder(ResourceBuilder
            .CreateDefault()
            .AddService(typeof(WorkerBackgroundService).Namespace))
        .AddSource(typeof(WorkerBackgroundService).Namespace)
        .AddZipkinExporter(o =>
        {
            o.Endpoint = new Uri(configuration["Zipkin:Url"]);
        })
    );
```

## Running the project

Inside [src folder](./src), type the command below to up all containers (`first-api`, `second-api`, `worker`, `rabbit` and `zipkin`):

```bash
  docker-compose up
```

wait for all containers get on and then send a request to the `first-api`:

```bash
curl --request POST \
  --url http://localhost:5000/WeatherForecast \
  --header 'Content-Type: application/json' \
  --header 'accept: */*' \
  --data '{
	"temperatureC": 10,
	"summary": "Trace Test"
}'
```

the message that you sent above will travel throughout the flow (`first-api` > `second-api` >  `rabbit` > `worker`) along with the propagation fields (`traceparent` and `tracestate`). To see the generated distributed tracing diagram, access `zipkin` in your browser:

```bash
  http://localhost:9411/
```

at home page, let the search field empty and type `RUN QUERY` to load all traces. Finally, click in your trace, then you'll see a diagram like this:

![Zipkin Diagram](zipkin-diagram.png)

## Trace context propagation through http calls

As the standard recommends, fields `traceparent` and `tracestate` SHOULD be added in the request header.

## Trace context propagation through AMQP calls

As the standard recommends, fields `traceparent` and `tracestate` SHOULD be added to the message in the `application-properties` section by message publisher. Message reader SHOULD construct the full trace context by reading `traceparent` and `tracestate` fields from the `message-annotations` first and if not exist - from `application-properties`.

Nevertheless, as Trace context for AMQP standard is very recent, not all packages and libraries are updated to deal with `traceparent` and `tracestate` propagation, that's the reason why OSS libraries like [NServiceBus](https://github.com/Particular/NServiceBus) or also [Opentelemetry](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/examples/MicroserviceExample/Utils/Messaging/MessageReceiver.cs#L91) add them in the message header. This application follows the libraries examples adding `traceparent` in the message header.
