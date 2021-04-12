# Dotnet Trace Context

System that shows a [W3C trace context](https://www.w3.org/TR/trace-context) and [AMQP W3C trace context](https://w3c.github.io/trace-context-amqp/#traceparent-amqp-format) example in .NET 5.

## Trace context propagation through http calls

Fields `traceparent` and `tracestate` SHOULD be added in the request header.

## Trace context propagation through AMQP calls

Fields `traceparent` and `tracestate` SHOULD be added to the message in the `application-properties` section by message publisher.

Message reader SHOULD construct the full trace context by reading `traceparent` and `tracestate` fields from the `message-annotations` first and if not exist - from `application-properties`.
