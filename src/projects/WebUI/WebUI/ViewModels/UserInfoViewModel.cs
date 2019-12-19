using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class UserInfoViewModel
    {
        [EmailAddress]
        public string Email { get; set; }

        public string Name { get; set; }

        public string Id { get; set; }

        public string LoginType { get; set; }

    }
}
