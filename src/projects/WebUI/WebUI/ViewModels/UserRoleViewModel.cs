using System.Collections.Generic;

namespace WebUI.ViewModels
{
    public class UserRoleViewModel
    {
        public UserRoleViewModel()
        {
            Users = new List<string>();
        }

        public string Email { get; set; }
        public string RoleId { get; set; }

        public List<string> Users { get; set; }
    }
}
