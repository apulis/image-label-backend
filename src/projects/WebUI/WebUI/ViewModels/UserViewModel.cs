using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class UserViewModel
    {
        public int id { get; set; }
        public string loginId { get; set; }
        public string email { get; set; }
        public string name { get; set; }
        public string loginType { get; set; }
        public ExternalLoginMessageViewModel externalLoginMessage { set; get; }
    }

    public class ExternalLoginMessageViewModel
    {
        public ExternalLoginMessageModel loginType { get; set; }
    }

    public class ExternalLoginMessageModel
    {
        public string id { get; set; }
        public string loginId { get; set; }
        public string email { get; set; }
        public string name { get; set; }
        public string prevUserId { get; set; }
    }

}
