// <copyright file="ValuesController.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
using Microsoft.AspNetCore.Mvc;

namespace TestApp.AspNetCore.Controllers.V1
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class ApiVersioningController : Controller
    {
        // GET api/v1/apiVersion
        [HttpGet]
        [MapToApiVersion("1.0")]
        public string Get()
        {
            return "version 1";
        }

        // GET api/v1/apiVersion/42
        [HttpGet("{id}")]
        [MapToApiVersion("1.0")]
        public string Get(int id)
        {
            return $"version 1 (id = {id})";
        }
    }
}
