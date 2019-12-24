using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class AddDatasetViewModel
    {
        public Guid dataSetId { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Info { get; set; }
        [Required]
        public string Type { get; set; }
    }
}
