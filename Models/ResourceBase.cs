using System;

namespace SOMIOD.Models
{
    public class ResourceBase
    {
        public string res_type { get; set; }
        public string resource_name { get; set; }
        public DateTime creation_datetime { get; set; }
    }
}