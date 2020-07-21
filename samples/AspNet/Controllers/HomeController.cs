using System.Web.Mvc;

namespace OpenTelemetry.Samples.AspNet.Controllers
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
