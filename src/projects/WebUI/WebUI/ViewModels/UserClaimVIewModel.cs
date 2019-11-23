using System.Collections.Generic;

namespace WebUI.ViewModels
{
    public class UserClaimViewModel
    {
        public string user_id { get; set; }
        public List<string> claims { get; set; }
    }
}
