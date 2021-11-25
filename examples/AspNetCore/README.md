# OpenTelemetry Example Application

This project is an example of ASP.NET Core instrumentation for tracing and logging.

The following exporters are configured for viewing the traces:
* Console
* Zipkin

The following exporters are configured for viewing the metrics:
* Console

## Running the example

A running instance of Zipkin is required. This can easily be spun up in docker containers.

The project can be run from this directory as follows:

```shell
dotnet run
```

Instead of running the project and its dependencies individually, if you are using Docker Desktop,
a `docker-compose` file is provided. This makes standing up the Zipkin dependencies easy, as well as starting the application.

To run the example using `docker-compose`, run the following from this
directory:

```shell
docker-compose up --build
```

With everything running:

* [Invoke the API](http://localhost:5000/WeatherForecast) to create new data.
* If you have run Zipkin with default settings:
  * View your traces with Zipkin [here](http://localhost:9411/zipkin)

## References

* [Docker Desktop](https://www.docker.com/products/docker-desktop)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
