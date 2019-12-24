using System.Collections.Generic;
using WebUI.ViewModels;

namespace WebUI.Models
{
    public class AccountModel
    {
        public string GUid { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public string Role { get; set; }
        public List<UserInfoViewModel> Users { get; set; }
        public List<DataSetModel> DataSets { get; set; }
    }
}
