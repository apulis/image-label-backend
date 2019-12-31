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
        public string Name { get; set; }
        [Required]
        public string Info { get; set; }
        [Required]
        public string Type { get; set; }
        [Required]
        public List<AddLabelViewModel> Labels { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Labels.Count==0)
            {
                yield return new ValidationResult(
                    "list can't be empty.",new[] { "labels" });
            }
        }
    }
}
