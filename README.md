# Dotnet Trace Context

System that shows a [W3C trace context](https://www.w3.org/TR/trace-context) and [AMQP W3C trace context](https://w3c.github.io/trace-context-amqp/#traceparent-amqp-format) example in `.NET 5`.

## Trace context propagation through http calls

As the standard recomends, fields `traceparent` and `tracestate` SHOULD be added in the request header.

## Trace context propagation through AMQP calls

As the standard recomends, fields `traceparent` and `tracestate` SHOULD be added to the message in the `application-properties` section by message publisher. Message reader SHOULD construct the full trace context by reading `traceparent` and `tracestate` fields from the `message-annotations` first and if not exist - from `application-properties`.

Nevertheless, as Trace context for AMQP standard is very recent, not all packages and libraries are updated to deal with `traceparent` and `tracestate` propagation, that's the reason why OSS libraries like [NServiceBus](https://github.com/Particular/NServiceBus) or also [Opentelemetry](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/examples/MicroserviceExample/Utils/Messaging/MessageReceiver.cs#L91) add them in the message header. This application follows the libraries examples adding `traceparent` in the message header.

## Application architecture

The purpose here is to propagate a message with `traceparent` id throw two api's and one worker usign [W3C trace context](https://www.w3.org/TR/trace-context) standard. The `first-api` calls the `second-api` by a http call, on the other hand, the `second-api` has an asynchronous communication with the `worker` by a message broker (I chose [rabbitmq](https://www.rabbitmq.com/) for that). Furthermore, I chose [zipkin](https://zipkin.io/) to be the default APM tool, being responsible for get the application traces and build the distributed tracing diagram.

![Distributed Trace](doc/w3c-trace-context.png)

### Locally via `dotnet` command line tool
