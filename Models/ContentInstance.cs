using System;

namespace SOMIOD.Models
{
    public class ContentInstance : ResourceBase
    {
        public string content_type { get; set; }
        public string content { get; set; }

        public ContentInstance()
        {
            res_type = "content-instance";
        }
    }
}