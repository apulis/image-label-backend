using System.Collections.Generic;

namespace WebUI.ViewModels
{
    public class AddClaimViewModel
    {
        public string user_id { get; set; }
        public string ClaimName { get; set; }
        public string accountId { get; set; }
        public List<UserClaimViewModel> claims { get; set; }
    }
}
