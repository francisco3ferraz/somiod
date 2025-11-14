using System.Web.Http;
using SOMIOD.Models;

namespace SOMIOD.Controllers
{
    [RoutePrefix("api/somiod")]
    public class SomiodController : ApiController
    {
        private string connectionString =
            SOMIOD.Properties.Settings.Default.ConnStr;

        // TODO: Implement CRUD operations for all resources
        // Following the RESTful patterns from Worksheet 03
    }
}