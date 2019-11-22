using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Heavy.Web.ViewModels
{
    public class UserRoleViewModel
    {
        public UserRoleViewModel()
        {
            Users = new List<string>();
        }

        public string UserName { get; set; }
        public string RoleId { get; set; }

        public List<string> Users { get; set; }
    }
}
