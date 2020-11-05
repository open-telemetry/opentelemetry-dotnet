// <copyright file="HomeController.cs" company="OpenTelemetry Authors">
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

using System.Web.Mvc;

namespace Examples.AspNet.Controllers
{
    public class HomeController : Controller
    {
        // For testing traditional routing. Ex: https://localhost:XXXX/
        public ActionResult Index()
        {
            return this.View();
        }

        [Route("about_attr_route/{customerId}")] // For testing attribute routing. Ex: https://localhost:XXXX/about_attr_route
        public ActionResult About(int? customerId)
        {
            this.ViewBag.Message = $"Your application description page for customer {customerId}.";

            return this.View();
        }
    }
}
