using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class AccountViewModel
    {
        public string GUid { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public string Role { get; set; }
        public List<UserInfoViewModel> Users { get; set; }
        public List<DataSetViewModel> DataSets { get; set; }
    }
}
