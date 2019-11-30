using System.Collections.Generic;

namespace WebUI.ViewModels
{
    public class UserClaimViewModel
    {
        public string user_id { get; set; }
        public string GUid { get; set; }
        public string Name { get; set; }
        public List<UserClaimViewModel> claims { get; set; }
    }
}
