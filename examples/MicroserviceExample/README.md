# End-to-end example

This directory contains a suite of example applications that communicate with each other.

1. An ASP.NET Core Web API
2. A background Worker Service

The Web API publishes messages to RabbitMQ and the Worker Service consumes the messages.

Trace context propagation is achieved between the two applications using the .NET OpenTelemetry API.

Traces are exported to a containerized Zipkin instance.

## Running the sample applications

The sample applications can easily be run using Docker Desktop by running:

```shell
docker-compose up --build
```

Once the containers are up, invoke the Web API by navigating to http://localhost:5000/RabbitMq.
Your traces can be viewed by going to http://localhost:9411/zipkin/ and RabbitMQ can be managed at http://localhost:15672/ (user: guest, password: guest). 

## References

* [Docker Desktop](https://www.docker.com/products/docker-desktop)
* [Zipkin](https://zipkin.io)
* [RabbitMQ](https://www.rabbitmq.com/)
* [Worker Service](https://docs.microsoft.com/en-us/azure/azure-monitor/app/worker-service)
* [OpenTelemetry Project](https://opentelemetry.io/)
