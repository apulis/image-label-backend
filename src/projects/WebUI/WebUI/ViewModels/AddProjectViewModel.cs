using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class AddProjectViewModel
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Info { get; set; }
    }
}
