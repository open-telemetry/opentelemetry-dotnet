// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.AspNetCore

open System

type WeatherForecast =
    { Date: DateTime
      TemperatureC: int
      Summary: string }

    member this.TemperatureF =
        32.0 + (float this.TemperatureC / 0.5556)
