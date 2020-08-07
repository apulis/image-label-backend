using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class AddDatasetViewModel:IValidatableObject
    {
        public Guid dataSetId { get; set; }
        [Required]
        public string name { get; set; }
        [Required]
        public string info { get; set; }
        [Required]
        public string type { get; set; }
        [Required]
        public string dataSetBindId { get; set; }
        [Required]
        public string dataSetPath { get; set; }
        public bool isPrivate { get; set; }
        public string convertStatus { get; set; }
        public string convertOutPath { get; set; }

        public List<AddLabelViewModel> Labels { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Labels!=null && Labels.Count < 0)
            {
                yield return new ValidationResult(
                    "list can't be empty.", new[] { "labels" });
            }
        }
    }
}
