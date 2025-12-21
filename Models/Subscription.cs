using System;

namespace SOMIOD.Models
{
    public class Subscription : ResourceBase
    {
        public int evt { get; set; } // 1=creation, 2=deletion, 3=both
        public string endpoint { get; set; }

        public Subscription()
        {
            res_type = "subscription";
        }
    }
}