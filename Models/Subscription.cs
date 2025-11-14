namespace SOMIOD.Models
{
    public class Subscription : ResourceBase
    {
        public int evt { get; set; } // 1=creation, 2=deletion
        public string endpoint { get; set; }

        public Subscription()
        {
            res_type = "subscription";
        }
    }
}