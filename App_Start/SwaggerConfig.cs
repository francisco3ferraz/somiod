using somiod;
using Swashbuckle.Application;
using System.Linq;
using System.Web.Http;
using WebActivatorEx;

[assembly: PreApplicationStartMethod(typeof(SwaggerConfig), "Register")]

namespace somiod
{
    public class SwaggerConfig
    {
        public static void Register()
        {
            var thisAssembly = typeof(SwaggerConfig).Assembly;

            GlobalConfiguration.Configuration
                .EnableSwagger(c =>
                    {
                        c.SingleApiVersion("v1", "SOMIOD API");
                        c.IncludeXmlComments(GetXmlCommentsPath());
                    })
                .EnableSwaggerUi(c =>
                    {
                    });
        }

        protected static string GetXmlCommentsPath()
        {
            return System.String.Format(@"{0}\bin\SOMIOD.XML",
                System.AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
